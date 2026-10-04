targetScope = 'resourceGroup'

@description('Azure region for the deployment.')
param location string = resourceGroup().location

@description('Short application name used in Azure resource names.')
param appName string = 'kenya-location-api'

@description('Environment name.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

var resourcePrefix = '${appName}-${environment}'

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: '${resourcePrefix}-env'
  location: location
}
