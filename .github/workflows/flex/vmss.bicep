param appName string
param location string
param vnetSubnetId string
param appInsightsInstrumentationKey string
param appInsightsConnectionString string
param storageConnectionString string

param adminUsername string = 'azureuser'

var vmssName = '${appName}-vmss'

resource vmss 'Microsoft.Compute/virtualMachineScaleSets@2022-08-01' = {
  name: vmssName
  location: location
  sku: {
    name: 'Standard_B2pts_v2'
    capacity: 1
    tier: 'Standard'
  }
  properties: {
    virtualMachineProfile: {
      storageProfile: {
        imageReference: {
          publisher: 'Canonical'
          offer: 'UbuntuServer'
          sku: '22_04-lts-gen2'
          version: 'latest'
        }
      }
      osProfile: {
        computerNamePrefix: vmssName
        adminUsername: adminUsername
        linuxConfiguration: {
          disablePasswordAuthentication: true
          ssh: {
            publicKeys: [
              {
                path: '/home/${adminUsername}/.ssh/authorized_keys'
                keyData: '' // Replace with actual SSH public key or use a Key Vault reference
              }
            ]
          }
        }
      }
      networkProfile: {
        networkInterfaceConfigurations: [
          {
            name: 'vmssNic'
            properties: {
              primary: true
              ipConfigurations: [
                {
                  name: 'ipconfig1'
                  properties: {
                    subnet: {
                      id: vnetSubnetId
                    }
                  }
                }
              ]
            }
          }
        ]
      }
    }
    upgradePolicy: {
      mode: 'Manual'
    }
    orchestrationMode: 'Flexible'
  }
}

resource dockerExtension 'Microsoft.Compute/virtualMachineScaleSets/extensions@2024-07-01' {
  parent: vmss
  name: 'docker-extension'
  location: location
  properties: {
    publisher: 'Microsoft.Azure.Extensions'
    type: 'DockerExtension'
    typeHandlerVersion: '1.2'
    autoUpgradeMinorVersion: true
  }
}
