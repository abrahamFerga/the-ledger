output "resource_group" {
  value = azurerm_resource_group.main.name
}

output "api_fqdn" {
  description = "Public hostname of the API container app."
  value       = azurerm_container_app.api.ingress[0].fqdn
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}

output "storage_account" {
  value = azurerm_storage_account.main.name
}

output "key_vault" {
  value = azurerm_key_vault.main.name
}

output "identity_client_id" {
  value = azurerm_user_assigned_identity.app.client_id
}
