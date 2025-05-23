/********************************************************************************
 * Copyright (c) 2022 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.Extensions;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Identity;
using Org.Eclipse.TractusX.Portal.Backend.Framework.IO;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Web;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Apps.Service.BusinessLogic;

/// <summary>
/// Implementation of <see cref="IAppReleaseBusinessLogic"/>.
/// </summary>
public class AppReleaseBusinessLogic(
    IPortalRepositories portalRepositories,
    IOptions<AppsSettings> settings,
    IOfferService offerService,
    IOfferDocumentService offerDocumentService,
    IOfferSetupService offerSetupService,
    IIdentityService identityService) : IAppReleaseBusinessLogic
{
    private readonly AppsSettings _settings = settings.Value;
    private readonly IIdentityData _identityData = identityService.IdentityData;

    /// <inheritdoc/>
    public Task CreateAppDocumentAsync(Guid appId, DocumentTypeId documentTypeId, IFormFile document, CancellationToken cancellationToken) =>
        UploadAppDoc(appId, documentTypeId, document, OfferTypeId.APP, cancellationToken);

    private async Task UploadAppDoc(Guid appId, DocumentTypeId documentTypeId, IFormFile document, OfferTypeId offerTypeId, CancellationToken cancellationToken) =>
        await offerDocumentService.UploadDocumentAsync(appId, documentTypeId, document, offerTypeId, _settings.UploadAppDocumentTypeIds, OfferStatusId.CREATED, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);

    /// <inheritdoc/>
    public Task<IEnumerable<AppRoleData>> AddAppUserRoleAsync(Guid appId, IEnumerable<AppUserRole> userRoles)
    {
        AppExtensions.ValidateAppUserRole(appId, userRoles);
        return InsertAppUserRoleAsync(appId, userRoles);
    }

    private async Task<IEnumerable<AppRoleData>> InsertAppUserRoleAsync(Guid appId, IEnumerable<AppUserRole> userRoles)
    {
        var companyId = _identityData.CompanyId;
        var result = await portalRepositories.GetInstance<IOfferRepository>().IsProviderCompanyUserAsync(appId, companyId, OfferTypeId.APP).ConfigureAwait(ConfigureAwaitOptions.None);
        if (result == default)
        {
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_EXIST, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        if (!result.IsProviderCompanyUser)
        {
            throw ForbiddenException.Create(AppChangeErrors.APP_FORBIDDEN_COM_NOT_PROVIDER_COM_APP, new ErrorParameter[] { new("companyId", _identityData.CompanyId.ToString()), new("appId", appId.ToString()) });
        }

        var roleData = await AppExtensions.CreateUserRolesWithDescriptions(portalRepositories.GetInstance<IUserRolesRepository>(), appId, userRoles).ConfigureAwait(ConfigureAwaitOptions.None);

        // When user uploads the same role names which are already attached to an APP
        // no role will be added to the given appId and there's no need to update the Offer entity
        if (!roleData.Any())
            return roleData;

        portalRepositories.GetInstance<IOfferRepository>().AttachAndModifyOffer(appId, offer =>
            offer.DateLastChanged = DateTimeOffset.UtcNow);
        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        return roleData;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AgreementDocumentData> GetOfferAgreementDataAsync(string languageShortName) =>
        offerService.GetOfferTypeAgreements(OfferTypeId.APP, languageShortName);

    /// <inheritdoc/>
    public async Task<OfferAgreementConsent> GetOfferAgreementConsentById(Guid appId)
    {
        return await offerService.GetProviderOfferAgreementConsentById(appId, OfferTypeId.APP).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ConsentStatusData>> SubmitOfferConsentAsync(Guid appId, OfferAgreementConsent offerAgreementConsents)
    {
        if (appId == Guid.Empty)
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_APP_ID_NOT_EMPTY);
        }

        return offerService.CreateOrUpdateProviderOfferAgreementConsent(appId, offerAgreementConsents, OfferTypeId.APP);
    }

    /// <inheritdoc/>
    public async Task<AppProviderResponse> GetAppDetailsForStatusAsync(Guid appId, string languageShortName)
    {
        var result = await offerService.GetProviderOfferDetailsForStatusAsync(appId, OfferTypeId.APP, DocumentTypeId.APP_LEADIMAGE, languageShortName).ConfigureAwait(ConfigureAwaitOptions.None);
        if (result.UseCase == null)
        {
            throw UnexpectedConditionException.Create(AppReleaseErrors.APP_UNEXPECTED_USECASE_NOT_NULL);
        }

        return new AppProviderResponse(
            result.Title,
            result.Provider,
            result.LeadPictureId,
            result.ProviderName,
            result.UseCase,
            result.Descriptions,
            result.Agreements,
            result.SupportedLanguageCodes,
            result.Price,
            result.Images,
            result.ProviderUri,
            result.ContactEmail,
            result.ContactNumber,
            result.Documents,
            result.SalesManagerId,
            result.PrivacyPolicies,
            result.TechnicalUserProfile);
    }

    /// <inheritdoc/>
    public async Task DeleteAppRoleAsync(Guid appId, Guid roleId)
    {
        var companyId = _identityData.CompanyId;
        var appUserRole = await portalRepositories.GetInstance<IOfferRepository>().GetAppUserRoleUntrackedAsync(appId, companyId, OfferStatusId.CREATED, roleId).ConfigureAwait(ConfigureAwaitOptions.None);
        if (!appUserRole.IsProviderCompanyUser)
        {
            throw ForbiddenException.Create(AppChangeErrors.APP_FORBIDDEN_COM_NOT_PROVIDER_COM_APP, new ErrorParameter[] { new("companyId", _identityData.CompanyId.ToString()), new("appId", appId.ToString()) });
        }

        if (!appUserRole.OfferStatus)
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_APP_ID_IN_CREATED_STATE);
        }

        if (!appUserRole.IsRoleIdExist)
        {
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_ROLE_EXIST, new ErrorParameter[] { new("roleId", roleId.ToString()) });
        }

        portalRepositories.GetInstance<IUserRolesRepository>().DeleteUserRole(roleId);
        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<CompanyUserNameData> GetAppProviderSalesManagersAsync() =>
       portalRepositories.GetInstance<IUserRolesRepository>().GetUserDataByAssignedRoles(_identityData.CompanyId, _settings.SalesManagerRoles);

    /// <inheritdoc/>
    public Task<Guid> AddAppAsync(AppRequestModel appRequestModel)
    {
        var emptyLanguageCodes = appRequestModel.SupportedLanguageCodes.Where(string.IsNullOrWhiteSpace);
        if (emptyLanguageCodes.Any())
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_LANG_CODE_NOT_EMPTY, new ErrorParameter[] { new(nameof(appRequestModel.SupportedLanguageCodes), nameof(appRequestModel.SupportedLanguageCodes)) });
        }

        var emptyUseCaseIds = appRequestModel.UseCaseIds.Where(item => item == Guid.Empty);
        if (emptyUseCaseIds.Any())
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_USECASE_ID_NOT_EMPTY, new ErrorParameter[] { new(nameof(appRequestModel.UseCaseIds), nameof(appRequestModel.UseCaseIds)) });
        }

        return CreateAppAsync(appRequestModel);
    }

    private async Task<Guid> CreateAppAsync(AppRequestModel appRequestModel)
    {
        if (appRequestModel.SalesManagerId.HasValue)
        {
            await offerService.ValidateSalesManager(appRequestModel.SalesManagerId.Value, _settings.SalesManagerRoles).ConfigureAwait(ConfigureAwaitOptions.None);
        }

        var appRepository = portalRepositories.GetInstance<IOfferRepository>();
        var appId = appRepository.CreateOffer(OfferTypeId.APP, _identityData.CompanyId, app =>
        {
            app.Name = appRequestModel.Title;
            app.OfferStatusId = OfferStatusId.CREATED;
            if (appRequestModel.SalesManagerId.HasValue)
            {
                app.SalesManagerId = appRequestModel.SalesManagerId;
            }

            app.ContactEmail = appRequestModel.ContactEmail;
            app.ContactNumber = appRequestModel.ContactNumber;
            app.MarketingUrl = appRequestModel.ProviderUri;
            app.LicenseTypeId = LicenseTypeId.COTS;
            app.DateLastChanged = DateTimeOffset.UtcNow;
        }).Id;
        appRepository.AddOfferDescriptions(appRequestModel.Descriptions.Select(d =>
              (appId, d.LanguageCode, d.LongDescription, d.ShortDescription)));
        appRepository.AddAppLanguages(appRequestModel.SupportedLanguageCodes.Select(c =>
              (appId, c)));
        appRepository.AddAppAssignedUseCases(appRequestModel.UseCaseIds.Select(uc =>
              (appId, uc)));
        appRepository.AddAppAssignedPrivacyPolicies(appRequestModel.PrivacyPolicies.Select(pp =>
              (appId, pp)));
        var licenseId = appRepository.CreateOfferLicenses(appRequestModel.Price).Id;
        appRepository.CreateOfferAssignedLicense(appId, licenseId);

        try
        {
            await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
            return appId;
        }
        catch (Exception exception) when (exception.InnerException?.Message.Contains("violates foreign key constraint") ?? false)
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_INVALID_LANG_CODE_OR_USECASE_ID);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAppReleaseAsync(Guid appId, AppRequestModel appRequestModel)
    {
        var companyId = _identityData.CompanyId;
        var appData = await portalRepositories.GetInstance<IOfferRepository>()
            .GetAppUpdateData(
                appId,
                companyId,
                appRequestModel.SupportedLanguageCodes)
            .ConfigureAwait(ConfigureAwaitOptions.None);
        if (appData is null)
        {
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_EXIST, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        if (appData.OfferState != OfferStatusId.CREATED)
        {
            throw ConflictException.Create(AppReleaseErrors.APP_CONFLICT_APP_STATE_CANNOT_UPDATED, new ErrorParameter[] { new("offerState", appData.OfferState.ToString()) });
        }

        if (!appData.IsUserOfProvider)
        {
            throw ForbiddenException.Create(AppReleaseErrors.APP_FORBIDDEN_COMPANY_NOT_APP_PROVIDER, new ErrorParameter[] { new(nameof(companyId), companyId.ToString()) });
        }

        if (appRequestModel.SalesManagerId.HasValue)
        {
            await offerService.ValidateSalesManager(appRequestModel.SalesManagerId.Value, _settings.SalesManagerRoles).ConfigureAwait(ConfigureAwaitOptions.None);
        }

        var newSupportedLanguages = appRequestModel.SupportedLanguageCodes.Except(appData.Languages.Where(x => x.IsMatch).Select(x => x.Shortname));
        var existingLanguageCodes = await portalRepositories.GetInstance<ILanguageRepository>().GetLanguageCodesUntrackedAsync(newSupportedLanguages).ToListAsync().ConfigureAwait(false);
        if (newSupportedLanguages.Except(existingLanguageCodes).Any())
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_LANG_NOT_EXIST_IN_DB, new ErrorParameter[] { new(nameof(appRequestModel.SupportedLanguageCodes), nameof(appRequestModel.SupportedLanguageCodes)), new("existingLanguageCodes", string.Join(",", newSupportedLanguages.Except(existingLanguageCodes))) });
        }

        var appRepository = portalRepositories.GetInstance<IOfferRepository>();
        appRepository.AttachAndModifyOffer(
        appId,
        app =>
        {
            app.Name = appRequestModel.Title;
            app.SalesManagerId = appRequestModel.SalesManagerId;
            app.ContactEmail = appRequestModel.ContactEmail;
            app.ContactNumber = appRequestModel.ContactNumber;
            app.MarketingUrl = appRequestModel.ProviderUri;
        },
        app =>
        {
            app.Name = appData.Name;
            app.SalesManagerId = appData.SalesManagerId;
            app.ContactEmail = appData.ContactEmail;
            app.ContactNumber = appData.ContactNumber;
            app.MarketingUrl = appData.MarketingUrl;
            app.DateLastChanged = DateTimeOffset.UtcNow;
        });

        offerService.UpsertRemoveOfferDescription(appId, appRequestModel.Descriptions, appData.OfferDescriptions);
        UpdateAppSupportedLanguages(appId, newSupportedLanguages, appData.Languages.Where(x => !x.IsMatch).Select(x => x.Shortname), appRepository);

        appRepository.CreateDeleteAppAssignedUseCases(appId, appData.MatchingUseCases, appRequestModel.UseCaseIds);

        appRepository.CreateDeleteAppAssignedPrivacyPolicies(appId, appData.MatchingPrivacyPolicies, appRequestModel.PrivacyPolicies);

        offerService.CreateOrUpdateOfferLicense(appId, appRequestModel.Price, appData.OfferLicense);

        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    private static void UpdateAppSupportedLanguages(Guid appId, IEnumerable<string> newSupportedLanguages, IEnumerable<string> languagesToRemove, IOfferRepository appRepository)
    {
        appRepository.AddAppLanguages(newSupportedLanguages.Select(language => (appId, language)));
        appRepository.RemoveAppLanguages(languagesToRemove.Select(language => (appId, language)));
    }

    /// <inheritdoc/>
    public Task<Pagination.Response<InReviewAppData>> GetAllInReviewStatusAppsAsync(int page, int size, OfferSorting? sorting, OfferStatusIdFilter? offerStatusIdFilter) =>
        Pagination.CreateResponseAsync(page, size, 15,
            portalRepositories.GetInstance<IOfferRepository>()
                .GetAllInReviewStatusAppsAsync(GetOfferStatusIds(offerStatusIdFilter), sorting ?? OfferSorting.DateDesc));

    /// <inheritdoc/>
    public Task SubmitAppReleaseRequestAsync(Guid appId) =>
        offerService.SubmitOfferAsync(appId, OfferTypeId.APP, _settings.SubmitAppNotificationTypeIds, _settings.CatenaAdminRoles, _settings.SubmitAppDocumentTypeIds);

    /// <inheritdoc/>
    public Task ApproveAppRequestAsync(Guid appId) =>
        offerService.ApproveOfferRequestAsync(appId, OfferTypeId.APP, _settings.ApproveAppNotificationTypeIds, _settings.ApproveAppUserRoles, _settings.SubmitAppNotificationTypeIds, _settings.CatenaAdminRoles, (_settings.OfferSubscriptionAddress, _settings.OfferDetailAddress), _settings.ActivationUserRoles);

    private IEnumerable<OfferStatusId> GetOfferStatusIds(OfferStatusIdFilter? offerStatusIdFilter) =>
        offerStatusIdFilter switch
        {
            OfferStatusIdFilter.InReview => new[] { OfferStatusId.IN_REVIEW },
            _ => _settings.OfferStatusIds
        };

    /// <inheritdoc/>
    public PrivacyPolicyData GetPrivacyPolicyDataAsync() =>
        new(Enum.GetValues<PrivacyPolicyId>());

    /// <inheritdoc />
    public Task DeclineAppRequestAsync(Guid appId, OfferDeclineRequest data) =>
        offerService.DeclineOfferAsync(appId, data, OfferTypeId.APP, NotificationTypeId.APP_RELEASE_REJECTION, _settings.ServiceManagerRoles, _settings.AppOverviewAddress, _settings.SubmitAppNotificationTypeIds, _settings.CatenaAdminRoles);

    /// <inheritdoc />
    public async Task<InReviewAppDetails> GetInReviewAppDetailsByIdAsync(Guid appId)
    {
        var result = await portalRepositories.GetInstance<IOfferRepository>()
            .GetInReviewAppDataByIdAsync(appId, OfferTypeId.APP).ConfigureAwait(ConfigureAwaitOptions.None);

        if (result == default)
        {
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_FOUND_OR_INCORRECT_STATUS, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        return new InReviewAppDetails(
            result.id,
            result.title ?? Constants.ErrorString,
            result.leadPictureId,
            result.images,
            result.Provider,
            result.UseCases,
            result.Description,
            result.Documents.GroupBy(d => d.DocumentTypeId).ToDictionary(g => g.Key, g => g.Select(d => new DocumentData(d.DocumentId, d.DocumentName))),
            result.Roles,
            result.Languages,
            result.ProviderUri ?? Constants.ErrorString,
            result.ContactEmail,
            result.ContactNumber,
            result.LicenseTypeId,
            result.Price ?? Constants.ErrorString,
            result.Tags,
            result.MatchingPrivacyPolicies,
            result.OfferStatusId,
            result.TechnicalUserProfile.ToDictionary(g => g.TechnicalUserProfileId, g => g.UserRoles));
    }

    /// <inheritdoc />
    public Task DeleteAppDocumentsAsync(Guid documentId) =>
        offerService.DeleteDocumentsAsync(documentId, _settings.DeleteDocumentTypeIds, OfferTypeId.APP);

    /// <inheritdoc />
    public async Task DeleteAppAsync(Guid appId)
    {
        var companyId = _identityData.CompanyId;
        var (isValidApp, isOfferType, isOfferStatus, isProviderCompanyUser, appData) = await portalRepositories.GetInstance<IOfferRepository>().GetAppDeleteDataAsync(appId, OfferTypeId.APP, companyId, OfferStatusId.CREATED).ConfigureAwait(ConfigureAwaitOptions.None);
        if (!isValidApp)
        {
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_EXIST, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        if (!isProviderCompanyUser)
        {
            throw ForbiddenException.Create(AppChangeErrors.APP_FORBIDDEN_COM_NOT_PROVIDER_COM_APP, new ErrorParameter[] { new("companyId", _identityData.CompanyId.ToString()), new("appId", appId.ToString()) });
        }

        if (!isOfferStatus)
        {
            throw ConflictException.Create(AppReleaseErrors.APP_CONFLICT_APP_NOT_CREATED_STATE, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        if (!isOfferType)
        {
            throw ConflictException.Create(AppReleaseErrors.APP_CONFLICT_OFFER_APP_ID_NOT_OFFERTYPE_APP, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        if (appData == null)
        {
            throw UnexpectedConditionException.Create(AppReleaseErrors.APP_UNEXPECTED_APP_DATA_NOT_NULL);
        }

        portalRepositories.GetInstance<IOfferRepository>().RemoveOfferAssignedLicenses(appData.OfferLicenseIds.Select(licenseId => (appId, licenseId)));
        portalRepositories.GetInstance<IOfferRepository>().RemoveOfferAssignedUseCases(appData.UseCaseIds.Select(useCaseId => (appId, useCaseId)));
        portalRepositories.GetInstance<IOfferRepository>().RemoveOfferAssignedPrivacyPolicies(appData.PolicyIds.Select(policyId => (appId, policyId)));
        portalRepositories.GetInstance<IDocumentRepository>().RemoveDocuments(appData.DocumentIdStatus.Where(x => x.DocumentStatusId != DocumentStatusId.LOCKED).Select(x => x.DocumentId));
        portalRepositories.GetInstance<IOfferRepository>().RemoveOfferAssignedDocuments(appData.DocumentIdStatus.Select(x => (appId, x.DocumentId)));
        portalRepositories.GetInstance<IOfferRepository>().RemoveAppLanguages(appData.LanguageCodes.Select(language => (appId, language)));
        portalRepositories.GetInstance<IOfferRepository>().RemoveOfferTags(appData.TagNames.Select(tagName => (appId, tagName)));
        portalRepositories.GetInstance<IOfferRepository>().RemoveOfferDescriptions(appData.DescriptionLanguageShortNames.Select(languageShortName => (appId, languageShortName)));
        portalRepositories.GetInstance<IOfferRepository>().RemoveOffer(appId);
        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    /// <inheritdoc />
    public Task SetInstanceType(Guid appId, AppInstanceSetupData data)
    {
        if (data.IsSingleInstance)
        {
            data.InstanceUrl.EnsureValidHttpUrl(() => nameof(data.InstanceUrl));
        }
        else if (!string.IsNullOrWhiteSpace(data.InstanceUrl))
        {
            throw ControllerArgumentException.Create(AppReleaseErrors.APP_ARG_MULTI_INSTANCE_APP_URL_SET, new ErrorParameter[] { new("instanceUrl", nameof(data.InstanceUrl)) });
        }

        return SetInstanceTypeInternal(appId, data);
    }

    private async Task SetInstanceTypeInternal(Guid appId, AppInstanceSetupData data)
    {
        var companyId = _identityData.CompanyId;
        var result = await portalRepositories.GetInstance<IOfferRepository>()
            .GetOfferWithSetupDataById(appId, companyId, OfferTypeId.APP)
            .ConfigureAwait(ConfigureAwaitOptions.None);
        if (result == default)
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_EXIST, new ErrorParameter[] { new("appId", appId.ToString()) });

        if (!result.IsUserOfProvidingCompany)
            throw ForbiddenException.Create(AppReleaseErrors.APP_FORBIDDEN_COMP_ID_NOT_PROVIDER_COMPANY, new ErrorParameter[] { new("companyId", _identityData.CompanyId.ToString()) });

        if (result.OfferStatus is not OfferStatusId.CREATED)
            throw ConflictException.Create(AppReleaseErrors.APP_CONFLICT_NOT_IN_CREATED_STATE, new ErrorParameter[] { new("appId", appId.ToString()), new("offerStatusId", OfferStatusId.CREATED.ToString()) });

        await (result.SetupTransferData == null
            ? HandleAppInstanceCreation(appId, data)
            : HandleAppInstanceUpdate(appId, data, (result.OfferStatus, result.IsUserOfProvidingCompany, result.SetupTransferData, result.AppInstanceData))).ConfigureAwait(ConfigureAwaitOptions.None);

        await portalRepositories.SaveAsync().ConfigureAwait(ConfigureAwaitOptions.None);
    }

    private async Task HandleAppInstanceCreation(Guid appId, AppInstanceSetupData data)
    {
        portalRepositories.GetInstance<IOfferRepository>().CreateAppInstanceSetup(appId,
            entity =>
            {
                entity.IsSingleInstance = data.IsSingleInstance;
                entity.InstanceUrl = data.InstanceUrl;
            });

        if (data.IsSingleInstance)
        {
            await offerSetupService
                .SetupSingleInstance(appId, data.InstanceUrl!).ConfigureAwait(ConfigureAwaitOptions.None);
        }
    }

    private async Task HandleAppInstanceUpdate(
        Guid appId,
        AppInstanceSetupData data,
        (OfferStatusId OfferStatus, bool IsUserOfProvidingCompany, AppInstanceSetupTransferData SetupTransferData, IEnumerable<(Guid AppInstanceId, Guid ClientId, string ClientClientId)> AppInstanceData) result)
    {
        var existingData = result.SetupTransferData;
        var instanceTypeChanged = existingData.IsSingleInstance != data.IsSingleInstance;
        portalRepositories.GetInstance<IOfferRepository>().AttachAndModifyAppInstanceSetup(
            existingData.Id,
            appId,
            entity =>
            {
                entity.InstanceUrl = data.InstanceUrl;
                entity.IsSingleInstance = data.IsSingleInstance;
            },
            entity =>
            {
                entity.InstanceUrl = existingData.InstanceUrl;
                entity.IsSingleInstance = existingData.IsSingleInstance;
            });

        (Guid AppInstanceId, Guid ClientId, string ClientClientId) appInstance;
        switch (instanceTypeChanged)
        {
            case true when existingData.IsSingleInstance:
                appInstance = GetAndValidateSingleAppInstance(result.AppInstanceData);
                await offerSetupService
                    .DeleteSingleInstance(appInstance.AppInstanceId, appInstance.ClientId, appInstance.ClientClientId)
                    .ConfigureAwait(ConfigureAwaitOptions.None);
                break;

            case true when data.IsSingleInstance:
                await offerSetupService
                    .SetupSingleInstance(appId, data.InstanceUrl!)
                    .ConfigureAwait(ConfigureAwaitOptions.None);
                break;

            case false when data.IsSingleInstance && existingData.InstanceUrl != data.InstanceUrl:
                appInstance = GetAndValidateSingleAppInstance(result.AppInstanceData);
                await offerSetupService
                    .UpdateSingleInstance(appInstance.ClientClientId, data.InstanceUrl!)
                    .ConfigureAwait(ConfigureAwaitOptions.None);
                break;
        }
    }

    private static (Guid AppInstanceId, Guid ClientId, string ClientClientId) GetAndValidateSingleAppInstance(IEnumerable<(Guid AppInstanceId, Guid ClientId, string ClientClientId)> appInstanceData)
    {
        if (appInstanceData.Count() != 1)
        {
            throw ConflictException.Create(AppReleaseErrors.APP_CONFLICT_ONLY_ONE_APP_INSTANCE_ALLOWED);
        }

        return appInstanceData.Single();
    }

    /// <inheritdoc />
    public Task<IEnumerable<TechnicalUserProfileInformation>> GetTechnicalUserProfilesForOffer(Guid offerId) =>
        offerService.GetTechnicalUserProfilesForOffer(offerId, OfferTypeId.APP, _settings.DimUserRoles, _settings.UserRolesAccessibleByProviderOnly);

    /// <inheritdoc />
    public Task UpdateTechnicalUserProfiles(Guid appId, IEnumerable<TechnicalUserProfileData> data) =>
        offerService.UpdateTechnicalUserProfiles(appId, OfferTypeId.APP, data, _settings.TechnicalUserProfileClient, _settings.UserRolesAccessibleByProviderOnly);

    /// <inheritdoc />
    public async Task<IEnumerable<ActiveAppRoleDetails>> GetAppProviderRolesAsync(Guid appId, string? languageShortName)
    {
        var (isValid, isProvider, roleDetails) = await portalRepositories.GetInstance<IUserRolesRepository>().GetOfferProviderRolesAsync(appId, OfferTypeId.APP, _identityData.CompanyId, languageShortName, Constants.DefaultLanguage).ConfigureAwait(ConfigureAwaitOptions.None);
        if (!isValid)
        {
            throw NotFoundException.Create(AppReleaseErrors.APP_NOT_EXIST, new ErrorParameter[] { new("appId", appId.ToString()) });
        }

        if (!isProvider)
        {
            throw ForbiddenException.Create(AppReleaseErrors.APP_FORBIDDEN_COMP_ID_NOT_PROVIDER_COMPANY, new ErrorParameter[] { new("companyId", _identityData.CompanyId.ToString()) });
        }

        return roleDetails ?? throw UnexpectedConditionException.Create(AppReleaseErrors.APP_UNEXPECT_ROLE_DETAILS_NOT_NULL);
    }
}
