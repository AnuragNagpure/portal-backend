/********************************************************************************
 * Copyright (c) 2022 BMW Group AG
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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;

public record CompanyServiceAccountDetailedData(
    Guid ServiceAccountId,
    string? ClientClientId,
    string Name,
    string Description,
    UserStatusId Status,
    IEnumerable<UserRoleData> UserRoleDatas,
    CompanyServiceAccountTypeId CompanyServiceAccountTypeId,
    CompanyServiceAccountKindId CompanyServiceAccountKindId,
    ConnectorResponseData? ConnectorData,
    OfferResponseData? OfferSubscriptionData,
    CompanyLastEditorData? CompanyLastEditorData,
    DimServiceAccountData? DimServiceAccountData);

public record ConnectorResponseData(Guid Id, string Name);

public record OfferResponseData(Guid Id, OfferTypeId Type, string? Name, Guid? SubscriptionId);

public record CompanyLastEditorData(string? Name, string CompanyName);

public record DimServiceAccountData(
    string AuthenticationServiceUrl,
    byte[] ClientSecret,
    byte[]? InitializationVector,
    int EncryptionMode
);
