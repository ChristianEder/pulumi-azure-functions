﻿using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Storage;
using System;
using System.Text.RegularExpressions;

namespace Pulumi.AzureFunctions.Sdk
{

    public class ArchiveFunctionApp : FunctionApp
    {
        public ArchiveFunctionApp(string name, ArchiveFunctionAppArgs args, CustomResourceOptions options = null) : base(name, Map(name, args), options)
        {
        }

        private static FunctionAppArgs Map(string name, ArchiveFunctionAppArgs args)
        {
            if(args.AppServicePlanId == null)
            {
                var plan = new Plan(name, new PlanArgs
                {
                    ResourceGroupName = args.ResourceGroupName,
                    Kind = "FunctionApp",
                    Sku = new PlanSkuArgs
                    {
                        Tier = "Dynamic",
                        Size = "Y1"
                    }
                });
                args.AppServicePlanId = plan.Id;
            }

            if (args.StorageAccount == null)
            {
                args.StorageAccount = new Account(MakeSafeStorageAccountName(name), new AccountArgs
                {
                    ResourceGroupName = args.ResourceGroupName,
                    AccountReplicationType = "LRS",
                    AccountTier = "Standard",
                    AccountKind = "StorageV2"
                });
            }

            if(args.StorageContainer == null)
            {
                args.StorageContainer = new Container(MakeSafeStorageContainerName(name), new ContainerArgs
                {
                    StorageAccountName = args.StorageAccount.Apply(a => a.Name),
                    ContainerAccessType = "private"
                });
            }

            var mapped = new FunctionAppArgs
            {
                StorageAccountName = args.StorageAccount.Apply(a => a.Name),
                StorageAccountAccessKey = args.StorageAccount.Apply(a => a.PrimaryAccessKey),
                SiteConfig = args.SiteConfig,
                ResourceGroupName = args.ResourceGroupName,
                OsType = args.OsType,
                Name = args.Name,
                Location = args.Location,
                Tags = args.Tags,
                Identity = args.Identity,
                Enabled = args.Enabled,
                EnableBuiltinLogging = args.EnableBuiltinLogging,
                DailyMemoryTimeQuota = args.DailyMemoryTimeQuota,
                ConnectionStrings = args.ConnectionStrings,
                ClientAffinityEnabled = args.ClientAffinityEnabled,
                AuthSettings = args.AuthSettings,
                AppSettings = args.AppSettings,
                AppServicePlanId = args.AppServicePlanId,
                HttpsOnly = args.HttpsOnly,
                Version = args.Version
            };

            var blob = new Blob(name, new BlobArgs
            {
                StorageAccountName = args.StorageAccount.Apply(a => a.Name),
                StorageContainerName = args.StorageContainer.Apply(a => a.Name),
                Source = args.Archive.Apply(a => (AssetOrArchive)a),
                Type = "Block"
            });

            if (mapped.AppSettings == null)
            {
                mapped.AppSettings = new InputMap<string>();
            }

            mapped.AppSettings.Add("WEBSITE_RUN_FROM_PACKAGE", GetSignedBlobUrl(blob, args.StorageAccount));
            return mapped;
        }

        private static string MakeSafeStorageAccountName(string name)
        {
            var regex = new Regex("[^a-zA-Z0-9]");
            var replaced = regex.Replace(name, "").ToLowerInvariant();
            return TrimLength(replaced, 24 - 8);
        }

        private static string MakeSafeStorageContainerName(string name)
        {
            var regex = new Regex("[^a-zA-Z0-9-]");
            var replaced = regex.Replace(name, "").ToLowerInvariant();
            return TrimLength(replaced, 63 - 8);
        }

        private static string TrimLength(string name, int length)
        {
            if(name.Length < length)
            {
                return name;
            }

            return name.Substring(0, length);
        }

        private static Output<string> GetSignedBlobUrl(Blob blob, Input<Account> storage)
        {
            const string signatureExpiration = "2100-01-01";


            var url = Output.All(new[] { storage.Apply(s => s.Name), storage.Apply(s => s.PrimaryConnectionString), blob.StorageContainerName, blob.Name })
            .Apply(async (parameters) =>
            {
                var accountName = parameters[0];
                var connectionString = parameters[1];
                var containerName = parameters[2];
                var blobName = parameters[3];

                var sas = await GetAccountBlobContainerSAS.InvokeAsync(new GetAccountBlobContainerSASArgs
                {
                    ConnectionString = connectionString,
                    ContainerName = containerName,
                    Start = "2020-07-20",
                    Expiry = signatureExpiration,
                    Permissions = new Azure.Storage.Inputs.GetAccountBlobContainerSASPermissionsArgs
                    {
                        Read = true,
                        Write = false,
                        Delete = false,
                        List = false,
                        Add = false,
                        Create = false
                    }
                });
                return $"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}{sas.Sas}";
            });

            return url;

        }
    }

    public class ArchiveFunctionAppArgs : ResourceArgs
    {
        // Summary:
        //     The backend storage account which will be used by this Function App (such
        //     as the dashboard, logs, and to deploy the function to).
        [Input("storageAccount", true, false)]
        public Input<Account>? StorageAccount { get; set; }

        // Summary:
        //     The backend storage container which will be used to deploy this Function App 
        [Input("storageAccountContainer", true, false)]
        public Input<Container>? StorageContainer { get; set; }

        // Summary:
        //     The archive which will be used to deploy this Function App 
        [Input("archive", true, false)]
        public Input<Archive>? Archive { get; set; }

        //
        // Summary:
        //     A `site_config` object as defined below.
        [Input("siteConfig", false, false)]
        public Input<FunctionAppSiteConfigArgs>? SiteConfig { get; set; }
        //
        // Summary:
        //     The name of the resource group in which to create the Function App.
        [Input("resourceGroupName", true, false)]
        public Input<string> ResourceGroupName { get; set; }
        //
        // Summary:
        //     A string indicating the Operating System type for this function app.
        [Input("osType", false, false)]
        public Input<string>? OsType { get; set; }
        //
        // Summary:
        //     Specifies the name of the Function App. Changing this forces a new resource to
        //     be created.
        [Input("name", false, false)]
        public Input<string>? Name { get; set; }
        //
        // Summary:
        //     Specifies the supported Azure location where the resource exists. Changing this
        //     forces a new resource to be created.
        [Input("location", false, false)]
        public Input<string>? Location { get; set; }
        //
        // Summary:
        //     A mapping of tags to assign to the resource.
        public InputMap<string> Tags { get; set; }
        //
        // Summary:
        //     An `identity` block as defined below.
        [Input("identity", false, false)]
        public Input<FunctionAppIdentityArgs>? Identity { get; set; }
        //
        // Summary:
        //     Is the Function App enabled?
        [Input("enabled", false, false)]
        public Input<bool>? Enabled { get; set; }
        //
        // Summary:
        //     Should the built-in logging of this Function App be enabled? Defaults to `true`.
        [Input("enableBuiltinLogging", false, false)]
        public Input<bool>? EnableBuiltinLogging { get; set; }
        //
        // Summary:
        //     The amount of memory in gigabyte-seconds that your application is allowed to
        //     consume per day. Setting this value only affects function apps under the consumption
        //     plan. Defaults to `0`.
        [Input("dailyMemoryTimeQuota", false, false)]
        public Input<int>? DailyMemoryTimeQuota { get; set; }
        //
        // Summary:
        //     An `connection_string` block as defined below.
        public InputList<FunctionAppConnectionStringArgs> ConnectionStrings { get; set; }
        //
        // Summary:
        //     Should the Function App send session affinity cookies, which route client requests
        //     in the same session to the same instance?
        [Input("clientAffinityEnabled", false, false)]
        public Input<bool>? ClientAffinityEnabled { get; set; }
        //
        // Summary:
        //     A `auth_settings` block as defined below.
        [Input("authSettings", false, false)]
        public Input<FunctionAppAuthSettingsArgs>? AuthSettings { get; set; }
        //
        // Summary:
        //     A map of key-value pairs for [App Settings](https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings)
        //     and custom values.
        public InputMap<string> AppSettings { get; set; }
        //
        // Summary:
        //     The ID of the App Service Plan within which to create this Function App.
        [Input("appServicePlanId", true, false)]
        public Input<string> AppServicePlanId { get; set; }
        //
        // Summary:
        //     Can the Function App only be accessed via HTTPS? Defaults to `false`.
        [Input("httpsOnly", false, false)]
        public Input<bool>? HttpsOnly { get; set; }
        //
        // Summary:
        //     The runtime version associated with the Function App. Defaults to `~1`.
        [Input("version", false, false)]
        public Input<string>? Version { get; set; }
    }

}
