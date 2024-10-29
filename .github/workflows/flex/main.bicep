targetScope = 'subscription'

param appName string
param location string

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${appName}'
  location: location
}

module storageModule 'storage.bicep' = {
  scope: rg
  name: 'orleansStorageModule'
  params: {
    name: '${appName}storage'
    location: location
  }
}

module logsModule 'logs-and-insights.bicep' = {
  scope: rg
  name: 'orleansLogModule'
  params: {
    operationalInsightsName: '${appName}-logs'
    appInsightsName: '${appName}-insights'
    location: location
  }
}

module vnet 'network.bicep' = {
  scope: rg
  name: 'vnetModule'
  params: {
    appName: appName
    location: location
  }
}

module siloModule 'app-service.bicep' = {
  scope: rg
  name: 'orleansSiloModule'
  params: {
    appName: appName
    location: location
    vnetSubnetId: vnet.outputs.subnetId
    appInsightsConnectionString: logsModule.outputs.appInsightsConnectionString
    appInsightsInstrumentationKey: logsModule.outputs.appInsightsInstrumentationKey
    storageConnectionString: storageModule.outputs.connectionString
  }
}
