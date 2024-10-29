param appName string
param location string
param vnetSubnetId string
param appInsightsInstrumentationKey string
param appInsightsConnectionString string
param storageConnectionString string

resource appServicePlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2021-03-01' = {
  name: appName
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    virtualNetworkSubnetId: vnetSubnetId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'dotnet|8.0'
      netFrameworkVersion: 'v8.0'
      vnetPrivatePortsCount: 2
      webSocketsEnabled: true
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'Orleans__ClusterId'
          value: 'default'
        }
        {
          name: 'Orleans__Clustering__ProviderType'
          value: 'AzureTableStorage'
        }
        {
          name: 'Orleans__Clustering__ServiceKey'
          value: 'tables'
        }
        {
          name: 'Orleans__EnableDistributedTracing'
          value: 'true'
        }
        {
          name: 'Orleans__Endpoints__GatewayPort'
          value: '8001'
        }
        {
          name: 'Orleans__Endpoints__SiloPort'
          value: '8000'
        }
        {
          name: 'Orleans__GrainStorage__Default__ProviderType'
          value: 'AzureTableStorage'
        }
        {
          name: 'Orleans__GrainStorage__Default__ServiceKey'
          value: 'tables'
        }
        {
          name: 'Orleans__GrainStorage__PubSubStore__ProviderType'
          value: 'AzureTableStorage'
        }
        {
          name: 'Orleans__GrainStorage__PubSubStore__ServiceKey'
          value: 'tables'
        }
        {
          name: 'Orleans__ServiceId'
          value: 'Lenderboxd'
        }
        {
          name: 'ConnectionStrings__queues'
          value: storageConnectionString
        }
        {
          name: 'ConnectionStrings__tables'
          value: storageConnectionString
        }
      ]
      alwaysOn: true
    }
  }
}

resource slotConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  name: 'slotConfigNames'
  parent: appService
  properties: {
    appSettingNames: [
      'ORLEANS_CLUSTER_ID'
    ]
  }
}

resource appServiceConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  parent: appService
  name: 'metadata'
  properties: {
    CURRENT_STACK: 'dotnet'
  }
}
