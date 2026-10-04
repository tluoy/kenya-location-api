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
