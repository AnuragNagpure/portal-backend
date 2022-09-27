/********************************************************************************
 * Copyright (c) 2021,2022 BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the CatenaX (ng) GitHub Organisation.
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

using CatenaX.NetworkServices.PortalBackend.PortalEntities.Auditing;

namespace CatenaX.NetworkServices.PortalBackend.PortalEntities.Entities;

public class CompanyUserAssignedRole : IAuditable
{
    protected CompanyUserAssignedRole() {}

    /// <summary>
    /// Please only use for update or removing the entity from the database
    /// </summary>
    /// <param name="id">Id of the <see cref="CompanyUserAssignedRole"/></param>
    public CompanyUserAssignedRole(Guid id)
    {
        Id = id;
    }

    public CompanyUserAssignedRole(Guid companyUserId, Guid userRoleId)
    {
        CompanyUserId = companyUserId;
        UserRoleId = userRoleId;
    }
    
    public Guid Id { get; set; }
    public Guid CompanyUserId { get; private set; }
    public Guid UserRoleId { get; private set; }
    
    /// <inheritdoc />
    public Guid? LastEditorId { get; set; }
    // Navigation properties
    public virtual CompanyUser? CompanyUser { get; private set; }
    public virtual UserRole? UserRole { get; private set; }
}
