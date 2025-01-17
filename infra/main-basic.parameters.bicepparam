using 'main-basic.bicep'

@description('ID of the service principal that will be granted access to the Key Vault')
param principalId = 'af35198e-8dc7-4a2e-a41e-b2ba79bebd51'

@description('If you have an existing Cog Services Account, provide the name here')
param existingCogServicesName = ''

@description('If you have an existing Container Registry Account, provide the name here')
param existingContainerRegistryName = ''

param environmentName = 'CI'
param location = 'eastus2'
param backendExists = false
param backendDefinition = {
  settings: []
}
param appContainerAppEnvironmentWorkloadProfileName = 'app'
param containerAppEnvironmentWorkloadProfiles = [
  {
    name: 'app'
    workloadProfileType: 'D4'
    minimumCount: 1
    maximumCount: 10
  }
]

param useManagedIdentityResourceAccess = true

param azureChatGptStandardDeploymentName = 'chat'
param azureChatGptPremiumDeploymentName = 'chat-gpt4'
param azureEmbeddingDeploymentName = 'text-embedding'

@description('If you have an existing VNET to use, provide the name here')
param virtualNetworkName = ''
param virtualNetworkResourceGroupName = ''
param privateEndpointSubnetName = ''
param privateEndpointSubnetAddressPrefix = ''
param containerAppSubnetName = ''
param containerAppSubnetAddressPrefix = ''
