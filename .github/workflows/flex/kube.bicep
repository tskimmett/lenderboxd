param appName string
param location string
param vnetSubnetId string

@description('The number of nodes for the cluster.')
@minValue(1)
@maxValue(5)
param agentCount int = 1

resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: 'aks-cluster'
  sku: {
    name: 'Base'
    tier: 'Free'
  }
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: 'aks${appName}'
    agentPoolProfiles: [
      {
        name: 'agentpool'
        osDiskSizeGB: 0
        count: agentCount
        vmSize: 'Standard_B4pls_v2'
        osType: 'Linux'
        mode: 'System'
        vnetSubnetID: vnetSubnetId
      }
    ]
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: 'acr${uniqueString(appName)}'
  location: location
  sku: {
    name: 'Basic'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    adminUserEnabled: false
  }
}

var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, aks.id, acrPullRoleDefinitionId)
  scope: acr
  properties: {
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    roleDefinitionId: acrPullRoleDefinitionId
    principalType: 'ServicePrincipal'
  }
}
