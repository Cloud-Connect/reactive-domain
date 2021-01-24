﻿using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Policy;

namespace ReactiveDomain.Users.ReadModels
{
    // for use in applications enforcing security policy
    /// <summary>
    /// A read model that contains a synchronized security policy for the application. 
    /// </summary>
    public class SecurityPolicyRM :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<RoleMsgs.RoleCreated>,
        IHandle<RoleMsgs.RoleMigrated>,
        //IHandle<UserMsgs.UserCreated>,
        //IHandle<UserMsgs.Deactivated>,
        //IHandle<UserMsgs.Activated>,
        //IHandle<UserMsgs.RoleAssigned>,
        //IHandle<UserMsgs.AuthDomainUpdated>,
        //IHandle<UserMsgs.UserNameUpdated>,
        //IHandle<UserMsgs.RoleUnassigned>, 
        IHandle<RoleMsgs.ChildRoleAssigned>,
        IHandle<RoleMsgs.PermissionAdded>,
        IHandle<RoleMsgs.PermissionAssigned>
    {
        //public data
        public SecurityPolicy Policy;

        private readonly Dictionary<Guid, Permission> _permissions = new Dictionary<Guid, Permission>();
        private readonly Dictionary<Guid, Role> _roles = new Dictionary<Guid, Role>();
        private readonly Dictionary<Guid, Application> _applications = new Dictionary<Guid, Application>();
        // private readonly Dictionary<Guid, UserModel> _users = new Dictionary<Guid, UserModel>();
        //public List<UserModel> ActivatedUsers => _users.Values.Where(x => x.IsActivated).ToList(); //todo: is this the best way?

        //private string _userStream => new PrefixedCamelCaseStreamNameBuilder("pki_elbe").GenerateForCategory(typeof(User));
        /// <summary>
        /// Create a read model for a synchronized Security Policy.
        /// </summary>
        public SecurityPolicyRM(
            SecurityPolicy basePolicy,
            IConfiguredConnection conn,
            IDispatcher dispatcher)
            : base(nameof(ApplicationsRM), () => conn.GetListener(nameof(SecurityPolicyRM)))
        {
            Policy = basePolicy ?? new SecurityPolicyBuilder().Build();

            using (var appReader = conn.GetReader("AppReader"))
            {
                appReader.EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
                appReader.Read(typeof(ApplicationMsgs.ApplicationCreated));
            }
            Guid appId;
            var dbApp = _applications.Values.FirstOrDefault(
                app =>  string.CompareOrdinal(app.Name, Policy.ApplicationName) == 0 &&
                                string.CompareOrdinal(app.Version, Policy.ApplicationVersion) == 0);
            if (dbApp == null)
            {
                appId = Guid.NewGuid();
                var cmd = new ApplicationMsgs.CreateApplication(appId, Policy.ApplicationName,
                    Policy.ApplicationVersion);
                cmd.CorrelationId = cmd.Id;
                dispatcher.Send(cmd);
                //at this point we know the created app in the db matches the base policy, so no reason to read it 
            }
            else {
                appId = dbApp.Id;
            }
            Policy.App.UpdateApplicationDetails(appId);
            using (var appReader = conn.GetReader("AppReader"))
            {
                appReader.EventStream.Subscribe<RoleMsgs.RoleCreated>(this);
                //todo: subscribe to permissions
                //todo: subscribe role child mappings
                appReader.Read<ApplicationRoot>(appId);
            }

            foreach (var role in _roles.Values) {
                var baseRole = Policy.Roles.FirstOrDefault(r => r.Name == role.Name);
                if (baseRole == null) {
                    Policy.AddRole(role);
                }
                else {
                    baseRole.SetRoleId( role.RoleId);
                }
            }

            
            //todo: subscribe to user assignments

            //todo: subscribe to user stream???
            //Start<ApplicationRoot>(appId, blockUntilLive: true);
        }

        /// <summary>
        /// Checks whether the specified application is known to the system.
        /// </summary>
        /// <param name="id">The unique ID of the application.</param>
        /// <returns>True if the user exists.</returns>
        public bool ApplicationExists(Guid id)
        {
            return _applications.ContainsKey(id);
        }

        /// <summary>
        /// Checks whether the specified application is known to the system.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <returns></returns>
        public bool ApplicationExists(string name)
        {
            return _applications.Values.Any(x => x.Name == name);
        }

        /// <summary>
        /// Gets the unique ID of the specified application.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <returns>Application guid if a application with matching properties was found, otherwise empty guid.</returns>
        public Guid GetApplicationId(string name)
        {
            var app = _applications.Values.FirstOrDefault(x => x.Name == name);
            return app?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Given the name of the role and the application, returns whether the role exists or not.
        /// </summary>
        public bool RoleExists(
            string name,
            Guid applicationId)
        {
            return TryGetRoleId(name, applicationId, out _);
        }

        /// <summary>
        /// Gets the unique ID of the specified role.
        /// </summary>
        /// <param name="name">The of the role.</param>
        /// <param name="applicationId">The application for which this role is defined.</param>        
        /// <param name="id">The unique ID of the role. This is the out parameter</param>
        /// <returns>True if a role with matching properties was found, otherwise false.</returns>
        public bool TryGetRoleId(
            string name,
            Guid applicationId,
            out Guid? id)
        {
            id = null;
            var role = _roles.Values.FirstOrDefault(r => r.Application.Id == applicationId && string.CompareOrdinal(r.Name, name) == 0);
            if (role != null)
                id = role.RoleId;
            return id != null;
        }


        /// <summary>
        /// Given the application created event, adds a new application to the collection of roles.
        /// </summary>
        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {

            if (_applications.ContainsKey(@event.ApplicationId)) return;

            _applications.Add(
                @event.ApplicationId,
                new Application(
                    @event.ApplicationId,
                    @event.Name,
                    @event.ApplicationVersion
                ));
        }
        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleCreated @event)
        {
            if (_roles.ContainsKey(@event.RoleId)) return;
            if (!_applications.ContainsKey(@event.ApplicationId)) return; //todo: log error, this should never happen
            var app = _applications[@event.ApplicationId];

            _roles.Add(
                @event.RoleId,
                new Role(
                    @event.RoleId,
                    @event.Name,
                    app));
        }
        //todo: handle migration events
        /// <summary>
        /// Given the role migrated event, adds the role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleMigrated @event)
        {
            /*
            if(_roles.ContainsKey(@event.RoleId)) return;
            if(!_applications.ContainsKey(@event.ApplicationId)) return; //todo: log error, this should never happen
            var app = _applications[@event.ApplicationId];
            
            _roles.Add(
                @event.RoleId,
                new RoleModel(
                    @event.RoleId,
                    @event.Name,
                    app));
            */
        }

        public void Handle(RoleMsgs.ChildRoleAssigned @event)
        {
            if (!_roles.ContainsKey(@event.ParentRoleId)) return;
            if (!_roles.ContainsKey(@event.ChildRoleId)) return;

            _roles[@event.ParentRoleId].AddChildRole(_roles[@event.ChildRoleId]);
        }
        public void Handle(RoleMsgs.PermissionAdded @event)
        {
            if (_permissions.ContainsKey(@event.PermissionId)) return;
            if (!_applications.ContainsKey(@event.ApplicationId)) return; //todo: log this error
            var app = _applications[@event.ApplicationId];
            _permissions.Add(@event.PermissionId, new Permission(@event.PermissionId, @event.PermissionName, app));
        }
        public void Handle(RoleMsgs.PermissionAssigned @event)
        {
            if (!_roles.ContainsKey(@event.RoleId)) return; //todo: log this error
            if (!_permissions.ContainsKey(@event.PermissionId)) return; //todo: log this error
            _roles[@event.RoleId].AddPermission(_permissions[@event.PermissionId]);
        }

        /*
        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(UserMsgs.UserCreated @event) {
            if (_users.ContainsKey(@event.Id)) return;
            _users.Add(
                @event.Id,
                new UserModel(
                    @event.Id,
                    @event.UserName,
                    @event.SubjectId,
                    @event.AuthDomain));
        }
        public void Handle(UserMsgs.Deactivated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].IsActivated = false;
        }

        public void Handle(UserMsgs.Activated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].IsActivated = true;
        }

        public void Handle(UserMsgs.RoleAssigned @event)
        {
            if (!_users.ContainsKey(@event.UserId) || !_roles.ContainsKey(@event.RoleId)) return;
            var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
            if( role != null) return;
            _users[@event.UserId].Roles.Add(_roles[@event.RoleId]);
        }

        public void Handle(UserMsgs.RoleUnassigned @event)
        {
            if (!_users.ContainsKey(@event.UserId)|| !_roles.ContainsKey(@event.RoleId)) return;
            var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
            if( role == null) return;
            _users[@event.UserId].Roles.Remove(role);
        }
        public void Handle(UserMsgs.AuthDomainUpdated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].AuthDomain = @event.AuthDomain;
        }
        public void Handle(UserMsgs.UserNameUpdated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].UserName = @event.UserName;
        }
        public List<Role> RolesForUser(
            string subjectId,
            string userName,
            string authDomain,
            Application application)
        {
            var user = _users.Values.FirstOrDefault(x =>
                x.AuthDomain.Equals(authDomain, StringComparison.CurrentCultureIgnoreCase)
                && (string.IsNullOrEmpty(subjectId) ||
                    string.IsNullOrEmpty(x.SubjectId)
                    ? x.UserName.Equals(userName, StringComparison.CurrentCultureIgnoreCase)
                    : x.SubjectId.Equals(subjectId, StringComparison.CurrentCultureIgnoreCase))

            );
            if (user == null)
            {
                throw new UserNotFoundException("UserId = " + subjectId + ", authDomain = " + authDomain);
            }
            if (!user.IsActivated)
            {
                throw new UserDeactivatedException("UserId = " + subjectId + ", authDomain = " + authDomain);
            }
            return user.Roles.FindAll(x => x.Application == application);
        }
        */

    }
}