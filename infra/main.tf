terraform {
  required_version = ">= 1.6.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
  # backend "azurerm" {}  # configure remote state in CI (storage account + container)
}

provider "azurerm" {
  features {}
}

data "azurerm_client_config" "current" {}

resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

locals {
  suffix     = random_string.suffix.result
  db_conn    = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Database=ledgerdb;Username=${var.postgres_admin_login};Password=${var.postgres_admin_password};Ssl Mode=Require"
  redis_conn = "${azurerm_redis_cache.main.hostname}:${azurerm_redis_cache.main.ssl_port},password=${azurerm_redis_cache.main.primary_access_key},ssl=True,abortConnect=False"
}

resource "azurerm_resource_group" "main" {
  name     = "${var.name_prefix}-rg"
  location = var.location
  tags     = var.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "${var.name_prefix}-logs"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_app_environment" "main" {
  name                       = "${var.name_prefix}-env"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = var.tags
}

resource "azurerm_user_assigned_identity" "app" {
  name                = "${var.name_prefix}-id"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = var.tags
}

# --- Data plane -------------------------------------------------------------

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "${var.name_prefix}-pg-${local.suffix}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  administrator_login           = var.postgres_admin_login
  administrator_password        = var.postgres_admin_password
  sku_name                      = "B_Standard_B1ms"
  storage_mb                    = 32768
  public_network_access_enabled = true # tighten to private endpoint for production
  zone                          = "1"
  tags                          = var.tags
}

resource "azurerm_postgresql_flexible_server_database" "ledgerdb" {
  name      = "ledgerdb"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_redis_cache" "main" {
  name                 = "${var.name_prefix}-redis-${local.suffix}"
  resource_group_name  = azurerm_resource_group.main.name
  location             = azurerm_resource_group.main.location
  capacity             = 0
  family               = "C"
  sku_name             = "Basic"
  minimum_tls_version  = "1.2"
  non_ssl_port_enabled = false
  tags                 = var.tags
}

resource "azurerm_storage_account" "main" {
  name                     = "${var.name_prefix}st${local.suffix}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  tags                     = var.tags
}

resource "azurerm_storage_queue" "work" {
  name                 = "work"
  storage_account_name = azurerm_storage_account.main.name
}

resource "azurerm_storage_container" "statements" {
  name                  = "statements"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_key_vault" "main" {
  name                      = "${var.name_prefix}-kv-${local.suffix}"
  resource_group_name       = azurerm_resource_group.main.name
  location                  = azurerm_resource_group.main.location
  tenant_id                 = data.azurerm_client_config.current.tenant_id
  sku_name                  = "standard"
  enable_rbac_authorization = true
  tags                      = var.tags
}

resource "azurerm_cognitive_account" "openai" {
  count                 = var.enable_openai ? 1 : 0
  name                  = "${var.name_prefix}-openai-${local.suffix}"
  resource_group_name   = azurerm_resource_group.main.name
  location              = azurerm_resource_group.main.location
  kind                  = "OpenAI"
  sku_name              = "S0"
  custom_subdomain_name = "${var.name_prefix}-openai-${local.suffix}"
  tags                  = var.tags
}

# --- Managed-identity role assignments --------------------------------------

resource "azurerm_role_assignment" "kv_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

resource "azurerm_role_assignment" "blob" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

resource "azurerm_role_assignment" "queue" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

resource "azurerm_role_assignment" "openai" {
  count                = var.enable_openai ? 1 : 0
  scope                = azurerm_cognitive_account.openai[0].id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# --- Compute: scale-to-zero Container Apps ----------------------------------

resource "azurerm_container_app" "api" {
  name                         = "${var.name_prefix}-api"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app.id]
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  secret {
    name  = "db-connection"
    value = local.db_conn
  }
  secret {
    name  = "cache-connection"
    value = local.redis_conn
  }

  template {
    min_replicas = 0
    max_replicas = 5

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "ConnectionStrings__ledgerdb"
        secret_name = "db-connection"
      }
      env {
        name        = "ConnectionStrings__cache"
        secret_name = "cache-connection"
      }
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app.client_id
      }
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
    }
  }
}

resource "azurerm_container_app" "worker" {
  name                         = "${var.name_prefix}-worker"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app.id]
  }

  secret {
    name  = "db-connection"
    value = local.db_conn
  }
  secret {
    name  = "storage-connection"
    value = azurerm_storage_account.main.primary_connection_string
  }

  template {
    min_replicas = 0
    max_replicas = 5

    container {
      name   = "worker"
      image  = var.worker_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "ConnectionStrings__ledgerdb"
        secret_name = "db-connection"
      }
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app.client_id
      }
    }

    custom_scale_rule {
      name             = "queue-scaler"
      custom_rule_type = "azure-queue"
      metadata = {
        queueName   = azurerm_storage_queue.work.name
        queueLength = "5"
      }
      authentication {
        secret_name       = "storage-connection"
        trigger_parameter = "connection"
      }
    }
  }
}
