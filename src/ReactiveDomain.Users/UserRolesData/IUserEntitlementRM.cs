﻿using System.Collections.Generic;

namespace ReactiveDomain.Users.Identity.UserRolesData
{
    public interface IUserEntitlementRM
    {
        List<UserEntitlementRM.RoleModel> RolesForUser(string userSidFromAuthProvider, string userName, string authDomain, string application);
    }
}