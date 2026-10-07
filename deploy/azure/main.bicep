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

@description('API container image.')
param apiImage string = 'kenyalocationapidev.azurecr.io/kenya-location-api:dev'

@description('Ingestion container image.')
param ingestionImage string = 'kenyalocationapidev.azurecr.io/kenya-location-ingestion:dev'

var resourcePrefix = '${appName}-${environment}'
var registryName = replace('${appName}${environment}', '-', '')
var storageAccountName = 'kenyalocation${environment}data'
var databaseName = 'kenya_location'
var postgresHost = '${resourcePrefix}-db.postgres.database.azure.com'
var postgresConnectionString = 'Host=${postgresHost};Port=5432;Database=${databaseName};Username=${postgresAdminUser};Password=${postgresAdminPassword};Ssl Mode=Require'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource sourceDataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/source-data'
  properties: {
    publicAccess: 'None'
  }
}

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
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = {
  parent: postgresServer
  name: databaseName
  properties: {}
}

resource postgresFirewall 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = {
  parent: postgresServer
  name: 'allow-container-app'
  properties: {
    startIpAddress: '20.164.74.107'
    endIpAddress: '20.164.74.107'
  }
}

resource apiContainerApp 'Microsoft.App/containerApps@2025-01-01' = {
  name: resourcePrefix
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'postgres-connection-string'
          value: postgresConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: resourcePrefix
          image: apiImage
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection-string'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: '2980dcc3-3ea9-46c9-af9b-37d07c145cf3'
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
    principalId: apiContainerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource ingestionIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-ingestion-id'
  location: location
}

resource ingestionJob 'Microsoft.App/jobs@2025-01-01' = {
  name: '${resourcePrefix}-ingestion'
  location: location
  dependsOn: [
    ingestionAcrPull
    ingestionBlobReader
  ]
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${ingestionIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      triggerType: 'Manual'
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      replicaRetryLimit: 1
      replicaTimeout: 1800
      // Registry authentication is configured after the Job is created.
      // This avoids revision provisioning timeout during initial creation.
      secrets: [
        {
          name: 'database-url'
          value: 'postgresql://${postgresAdminUser}:${uriComponent(postgresAdminPassword)}@${postgresHost}:5432/${databaseName}?sslmode=require'
        }
      ]
    }
    template: {
      containers: [
        {
          name: '${resourcePrefix}-ingestion'
          image: ingestionImage
          env: [
            {
              name: 'GIS_ROOT'
              value: '/data/gis'
            }
            {
              name: 'PLACES_SOURCE_ZIP'
              value: '/data/places/13308773.zip'
            }
            {
              name: 'AZURE_STORAGE_ACCOUNT_URL'
              value: storageAccount.properties.primaryEndpoints.blob
            }
            {
              name: 'AZURE_STORAGE_CONTAINER'
              value: 'source-data'
            }
            {
              name: 'DATABASE_URL'
              secretRef: 'database-url'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

resource ingestionAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: '1f8c2ed7-7894-46d4-9cb8-0bd8e40614d3'
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
    principalId: ingestionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource ingestionBlobReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: '05d30ded-2e91-469d-889e-a0cec8d5c559'
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    )
    principalId: ingestionIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
