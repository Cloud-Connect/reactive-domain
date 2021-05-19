﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Policy;

namespace ReactiveDomain.Users.PolicyModel
{
    public sealed class SecurityPolicyBuilder : IDisposable
    {
        private readonly string _policyName;
        private readonly SecuredApplication _app;
        private readonly Dictionary<string, Role> _roles = new Dictionary<string, Role>();
        private readonly List<Type> _permissions = new List<Type>(); //hash or similar?
        private bool _disposed;
        private bool _isBuilt;
        private string _defaultRole;
        private Func<ClaimsPrincipal, User> _findUserFunc;

        public SecurityPolicyBuilder(string policyName = "default")
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                Assembly.GetEntryAssembly()?.GetName().Name,
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString());
        }

        public SecurityPolicyBuilder(string appName, Version appVersion, string policyName = "default")
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                appName,
                appVersion.ToString());
        }

        public SecurityPolicyBuilder AddRole(string roleName, params Action<RoleBuilder>[] roleActions)
        {
            if (_disposed || _isBuilt) throw new ObjectDisposedException(nameof(SecurityPolicyBuilder));
            var roleBuilder = new RoleBuilder(roleName, this);
            if (roleActions == null) return this;

            foreach (var roleAction in roleActions)
            {
                roleAction(roleBuilder);
            }
            return this;
        }

        public SecurityPolicyBuilder WithDefaultRole(string roleName)
        {
            _defaultRole = roleName;
            return this;
        }

        public SecurityPolicyBuilder WithUserResolver(Func<ClaimsPrincipal, User> findUserFunc)
        {
            _findUserFunc = findUserFunc;
            return this;
        }


        public class RoleBuilder
        {
            private readonly SecurityPolicyBuilder _policyBuilder;
            private readonly Role _role;
            public RoleBuilder(string name, SecurityPolicyBuilder policyBuilder)
            {
                _policyBuilder = policyBuilder;
                if (_policyBuilder._roles.ContainsKey(name)) throw new DuplicateRoleException(name, policyBuilder._policyName);
                _role = new Role(Guid.Empty, name, Guid.Empty);
                _policyBuilder._roles.Add(name, _role);
            }


            public RoleBuilder WithCommand(Type t)
            {
                _role.AllowPermission(t);

                return this;
            }
        }

        public SecurityPolicy Build()
        {
            _isBuilt = true;
            Role defaultRole = null;
            if (_roles.ContainsKey(_defaultRole ?? string.Empty))
            {
                defaultRole = _roles[_defaultRole];
            }
            else if (!string.IsNullOrWhiteSpace(_defaultRole))
            {
                defaultRole = new Role(Guid.Empty, _defaultRole, Guid.Empty);
                _roles.Add(_defaultRole, defaultRole);
            }

            var policy = new SecurityPolicy(
                                _policyName,
                                Guid.Empty,
                                _app,
                                _findUserFunc,
                                _roles.Values.ToList(),
                                defaultRole);
            Dispose();
            return policy;
        }

        public void Dispose()
        {
            _disposed = true;
            _roles.Clear();
            _permissions.Clear();
        }
    }


}