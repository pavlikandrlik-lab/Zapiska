using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Web;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
//using Microsoft.Exchange.WebServices.Data;

namespace ReportyFIS.Infrastructure
{
    public class ADConnector
    {
        // Zjištění všech informací z AD a vložení do třídy
        public static ADInfo GetUserInfoFromAD(string logonUser)
        {
            using (ADUser user = ConnectAD(logonUser))
            {
                if (user == null) return null;
                if (!user.HasGUID) return null;
                var downLevelLogonName = user.Sid.Translate(typeof(System.Security.Principal.NTAccount)).ToString();
                return new ADInfo()
                {
                    Guid = user.Guid.ToString(),
                    DisplayName = user.DisplayName,
                    FirstName = user.GivenName,
                    Surname = user.Surname,
                    Company = user.Company,
                    Department = user.Department,
                    Office = user.PhysicalDeliveryOfficeName,
                    Mail = user.EmailAddress,
                    EmployeeID = user.EmployeeId,
                    Telephone = user.VoiceTelephoneNumber,
                    Description = user.Description,
                    Login = downLevelLogonName //logonUser
                };
            }
        }
        
        
        // Připojení k AD acr
        private static ADUser ConnectAD(string logonUser)
        {
            using (var ctx = new PrincipalContext(ContextType.Domain, "acr"))
            {
                return ADUser.FindByIdentity(ctx, logonUser);
            }
        }

        // Vyhledání účtů
        public static List<Principal> GetADUsers(string find)
        {
            using (var ctx = new PrincipalContext(ContextType.Domain, "acr"))
            {
                using (var search = new PrincipalSearcher(new UserPrincipal(ctx) { DisplayName = find + "*", Enabled = true }))
                {
                    var data = search.FindAll().Take(15);
                    return data.ToList();
                }
            }
        }
    }

    public class ADInfo
    {
        public string Guid { get; set; }
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string Company { get; set; }
        public string Department { get; set; }
        public string Office { get; set; }
        public string Mail { get; set; }
        public string EmployeeID { get; set; }
        public string Telephone { get; set; }
        public string Description { get; set; }
        public string DN { get; set; }
        public string Path { get; set; }
    }

    // Zjištění bližších informací o uživateli z AD
    struct ADProps
    {
        internal const string UserCategory = "user)(objectCategory=person";
        internal const string PhysicalDeliveryOfficeName = "physicalDeliveryOfficeName";
        internal const string Department = "department";
        internal const string Company = "company";
    }

    [DirectoryObjectClass(ADProps.UserCategory)]
    [DirectoryRdnPrefix("CN")]
    public class ADUser : UserPrincipal
    {
        public ADUser(PrincipalContext context) : base(context)
        {
            //ExtensionSet se da pouzit pouze pro vyhledavani, objekt pak nelze ulozit
            //ExtensionSet(ADProps.ObjectCategory, ADProps.UserCategory);
        }

        public ADUser(PrincipalContext context, string samAccountName, string password, bool enabled) : base(context, samAccountName, password, enabled)
        {
            //ExtensionSet(ADProps.ObjectCategory, ADProps.UserCategory);
        }

        [DirectoryProperty(ADProps.PhysicalDeliveryOfficeName)]
        public string PhysicalDeliveryOfficeName
        {
            get
            {
                if (ExtensionGet(ADProps.PhysicalDeliveryOfficeName).Length != 1)
                    return null;

                return (string)ExtensionGet(ADProps.PhysicalDeliveryOfficeName)[0];
            }
            set
            {
                this.ExtensionSet(ADProps.PhysicalDeliveryOfficeName, value);
            }
        }

        [DirectoryProperty(ADProps.Department)]
        public string Department
        {
            get
            {
                if (ExtensionGet(ADProps.Department).Length != 1)
                    return null;

                return (string)ExtensionGet(ADProps.Department)[0];
            }
            set
            {
                this.ExtensionSet(ADProps.Department, value);
            }
        }

        [DirectoryProperty(ADProps.Company)]
        public string Company
        {
            get
            {
                if (ExtensionGet(ADProps.Company).Length != 1)
                    return null;

                return (string)ExtensionGet(ADProps.Company)[0];
            }
            set
            {
                this.ExtensionSet(ADProps.Company, value);
            }
        }

        public bool HasGUID
        {
            get { return Guid.HasValue; }
        }
        public bool HasEmailAddress
        {
            get { return !string.IsNullOrEmpty(EmailAddress); }
        }

        public static new ADUser FindByIdentity(PrincipalContext context, string identityValue)
        {
            return (ADUser)FindByIdentityWithType(context, typeof(ADUser), identityValue);
        }

        public static new ADUser FindByIdentity(PrincipalContext context, IdentityType identityType, string identityValue)
        {
            return (ADUser)FindByIdentityWithType(context, typeof(ADUser), identityType, identityValue);
        }
    }
}