﻿// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthPermissions.BaseCode;
using AuthPermissions.BaseCode.CommonCode;
using AuthPermissions.BaseCode.DataLayer.Classes;
using AuthPermissions.BaseCode.DataLayer.Classes.SupportTypes;
using AuthPermissions.BaseCode.DataLayer.EfCode;
using AuthPermissions.BaseCode.PermissionsCode;
using AuthPermissions.BaseCode.SetupCode;
using AuthPermissions.SetupCode;
using StatusGeneric;

namespace AuthPermissions.BulkLoadServices.Concrete
{
    /// <summary>
    /// This bulk loads Roles with their permissions from a string with contains a series of lines
    /// </summary>
    public class BulkLoadRolesService : IBulkLoadRolesService
    {
        private readonly AuthPermissionsDbContext _context;
        private readonly Type _enumPermissionType;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="options"></param>
        public BulkLoadRolesService(AuthPermissionsDbContext context, AuthPermissionsOptions options)
        {
            _context = context;
            _enumPermissionType = options.InternalData.EnumPermissionsType;
        }

        /// <summary>
        /// This allows you to add Roles with their permissions via the <see cref="BulkLoadRolesDto"/> class
        /// </summary>
        /// <param name="roleSetupData">A list of definitions containing the information for each Role</param>
        /// <returns>status</returns>
        public async Task<IStatusGeneric> AddRolesToDatabaseAsync(List<BulkLoadRolesDto> roleSetupData)
        {
            var status = new StatusGenericHandler();

            if (roleSetupData == null || !roleSetupData.Any())
                return status;

            foreach (var roleDefinition in roleSetupData)
            {
                var perRoleStatus = new StatusGenericHandler();
                var permissionNames = roleDefinition.PermissionsCommaDelimited
                    .Split(',').Select(x => x.Trim()).ToList();

                var roleType = roleDefinition.RoleType;
                //NOTE: If an advanced permission (i.e. has the display attribute has AutoGenerateFilter = true) is found the roleType is updated to HiddenFromTenant
                var packedPermissions = _enumPermissionType.PackPermissionsNamesWithValidation(permissionNames,
                    x => perRoleStatus.AddError(
                        $"The permission name '{x}' isn't a valid name in the {_enumPermissionType.Name} enum.",
                        nameof(permissionNames).CamelToPascal()), () => roleType = RoleTypes.HiddenFromTenant);

                status.CombineStatuses(perRoleStatus);
                if (perRoleStatus.IsValid)
                {
                    var role = new RoleToPermissions(roleDefinition.RoleName, roleDefinition.Description,
                        packedPermissions, roleType);
                    _context.Add(role);
                }
            }

            if (status.IsValid)
                status.CombineStatuses(await _context.SaveChangesWithChecksAsync());

            status.Message = $"Added {roleSetupData.Count} new RoleToPermissions to the auth database"; //If there is an error this message is removed
            return status;
        }
    }
}