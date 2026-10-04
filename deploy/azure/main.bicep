targetScope = 'resourceGroup'

@description('Azure region for the deployment.')
param location string = 'southafricanorth'

@description('Short application name used in Azure resource names.')
param appName string = 'kenya-location-api'

@description('Environment name.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('PostgreSQL administrator username.')
param postgresAdminUser string = 'location'

@description('PostgreSQL administrator password.')
@secure()
param postgresAdminPassword string

@description('PostgreSQL major version.')
param postgresVersion string = '17'

@description('PostgreSQL compute SKU.')
param postgresSkuName string = 'Standard_B1ms'

@description('PostgreSQL storage size in GiB.')
param postgresStorageSize int = 32

var resourcePrefix = '${appName}-${environment}'

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: '${resourcePrefix}-env'
  location: location
  properties: {}
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: '${resourcePrefix}-db'
  location: location
  sku: {
    name: postgresSkuName
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    version: postgresVersion
    storage: {
      storageSizeGB: postgresStorageSize
    }
  }
}
