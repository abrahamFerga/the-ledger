variable "name_prefix" {
  description = "Prefix for all resource names (lowercase, no spaces)."
  type        = string
  default     = "theledger"
}

variable "location" {
  description = "Azure region. Choose one with Azure OpenAI availability / acceptable data residency."
  type        = string
  default     = "eastus2"
}

variable "postgres_admin_login" {
  description = "PostgreSQL administrator login."
  type        = string
  default     = "ledgeradmin"
}

variable "postgres_admin_password" {
  description = "PostgreSQL administrator password (supply via TF_VAR / pipeline secret; never commit)."
  type        = string
  sensitive   = true
}

variable "api_image" {
  description = "Fully qualified API container image (registry/repo:tag)."
  type        = string
  default     = "mcr.microsoft.com/dotnet/samples:aspnetapp" # placeholder until CI pushes the real image
}

variable "worker_image" {
  description = "Fully qualified Worker container image (registry/repo:tag)."
  type        = string
  default     = "mcr.microsoft.com/dotnet/samples:aspnetapp"
}

variable "enable_openai" {
  description = "Provision an Azure OpenAI account for the LLM categorizer (ADR-0004)."
  type        = bool
  default     = false
}

variable "tags" {
  type    = map(string)
  default = { system = "the-ledger", managed_by = "terraform" }
}
