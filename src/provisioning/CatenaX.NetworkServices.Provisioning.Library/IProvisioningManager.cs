using CatenaX.NetworkServices.Provisioning.Library.Enums;
using CatenaX.NetworkServices.Provisioning.Library.Models;

namespace CatenaX.NetworkServices.Provisioning.Library;

public interface IProvisioningManager
{
    Task<string> GetNextCentralIdentityProviderNameAsync();
    Task<string> GetNextServiceAccountClientIdAsync();
    Task SetupSharedIdpAsync(string idpName, string organisationName);
    Task<string> CreateSharedUserLinkedToCentralAsync(string idpName, UserProfile userProfile);
    Task<IDictionary<string, IEnumerable<string>>> AssignClientRolesToCentralUserAsync(string centralUserId, IDictionary<string,IEnumerable<string>> clientRoleNames);
    Task<IEnumerable<string>> GetClientRolesAsync(string clientId);
    Task<IEnumerable<string>> GetClientRolesCompositeAsync(string clientId);
    Task<string> SetupOwnIdpAsync(string organisationName, string clientId, string metadataUrl, string clientAuthMethod, string? clientSecret);
    Task<string> CreateOwnIdpAsync(string organisationName, IamIdentityProviderProtocol providerProtocol);
    Task<string?> GetProviderUserIdForCentralUserIdAsync(string identityProvider, string userId);
    Task<IEnumerable<(string Alias, string UserId, string UserName)>> GetProviderUserLinkDataForCentralUserIdAsync(IEnumerable<string> identityProviders, string userId);
    Task AddProviderUserLinkToCentralUserAsync(string userId, string alias, string providerUserId, string providerUserName);
    Task DeleteProviderUserLinkToCentralUserAsync(string userId, string alias);
    Task<bool> UpdateSharedRealmUserAsync(string realm, string userId, string firstName, string lastName, string email);
    Task<bool> DeleteSharedRealmUserAsync(string idpName, string userIdShared);
    Task<bool> DeleteCentralRealmUserAsync(string userIdCentral);
    Task<string> SetupClientAsync(string redirectUrl);
    Task<ServiceAccountData> SetupCentralServiceAccountClientAsync(string clientId, ClientConfigRolesData config);
    Task UpdateCentralClientAsync(string internalClientId, ClientConfigData config);
    Task DeleteCentralClientAsync(string internalClientId);
    Task<ClientAuthData> GetCentralClientAuthDataAsync(string internalClientId);
    Task<ClientAuthData> ResetCentralClientAuthDataAsync(string internalClientId);
    Task AddBpnAttributetoUserAsync(string centralUserId, IEnumerable<string> bpns);
    Task<bool> ResetSharedUserPasswordAsync(string realm, string userId);
    Task<IEnumerable<string>> GetClientRoleMappingsForUserAsync(string userId, string clientId);
    Task<(string DisplayName, string RedirectUrl, string ClientId, bool Enabled, string AuthorizationUrl, IamIdentityProviderClientAuthMethod ClientAuthMethod)> GetCentralIdentityProviderDataOIDCAsync(string alias);
    Task UpdateCentralIdentityProviderDataOIDCAsync(string alias, string displayName, bool enabled, string authorizationUrl, IamIdentityProviderClientAuthMethod clientAuthMethod, string? secret = null);
    Task<(string DisplayName, string RedirectUrl, string ClientId, bool Enabled, string EntityId, string SingleSignOnServiceUrl)> GetCentralIdentityProviderDataSAMLAsync(string alias);
    Task UpdateCentralIdentityProviderDataSAMLAsync(string alias, string displayName, bool enabled, string entityId, string singleSignOnServiceUrl);
    Task DeleteCentralIdentityProviderAsync(string alias);
}
