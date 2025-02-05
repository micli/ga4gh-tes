﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TesApi.Web.Management.Clients;
using TesApi.Web.Management.Configuration;
using TesApi.Web.Management.Models.Quotas;
using TesApi.Web.Management.Models.Terra;

namespace TesApi.Web.Management
{
    /// <summary>
    /// Terra Batch Account quota provider.
    /// </summary>
    public class TerraQuotaProvider : IBatchQuotaProvider
    {
        private const string BatchAccountResourceType = @"Microsoft.Batch/batchAccounts";
        private const string SharedResourcePurpose = "SHARED_RESOURCE";

        private readonly TerraLandingZoneApiClient terraLandingZoneClient;
        private readonly Guid landingZoneId;

        /// <summary>
        /// Constructor of TerraQuotaProvider
        /// </summary>
        /// <param name="terraLandingZoneClient"></param>
        /// <param name="terraOptions"></param>
        /// <exception cref="ArgumentException"></exception>
        public TerraQuotaProvider(TerraLandingZoneApiClient terraLandingZoneClient, IOptions<TerraOptions> terraOptions)
        {
            ArgumentNullException.ThrowIfNull(terraOptions);
            ArgumentNullException.ThrowIfNull(terraLandingZoneClient);
            if (string.IsNullOrEmpty(terraOptions.Value.LandingZoneId))
            {
                throw new ArgumentException("The landing zone id is missing. Please check the app configuration.");
            }
            if (string.IsNullOrEmpty(terraOptions.Value.LandingZoneApiHost))
            {
                throw new ArgumentException("The landing zone id is missing. Please check the app configuration.");
            }

            this.terraLandingZoneClient = terraLandingZoneClient;
            landingZoneId = Guid.Parse(terraOptions.Value.LandingZoneId);
        }

        /// <inheritdoc />
        public async Task<BatchVmFamilyQuotas> GetQuotaForRequirementAsync(string vmFamily, bool lowPriority, int? coresRequirement)
        {
            ArgumentException.ThrowIfNullOrEmpty(vmFamily);

            var quotas = await GetBatchAccountQuotaFromTerraAsync();

            return ToVmFamilyBatchAccountQuotas(quotas, vmFamily, lowPriority, coresRequirement);
        }

        /// <inheritdoc />
        public async Task<BatchVmCoreQuota> GetVmCoreQuotaAsync(bool lowPriority)
        {
            var isDedicated = !lowPriority;
            var batchQuota = await GetBatchAccountQuotaFromTerraAsync();
            var isDedicatedAndPerVmFamilyCoreQuotaEnforced =
                isDedicated && batchQuota.QuotaValues.DedicatedCoreQuotaPerVmFamilyEnforced;
            var numberOfCores = lowPriority ? batchQuota.QuotaValues.LowPriorityCoreQuota : batchQuota.QuotaValues.DedicatedCoreQuota;

            List<BatchVmCoresPerFamily> dedicatedCoresPerFamilies = null;
            if (isDedicatedAndPerVmFamilyCoreQuotaEnforced)
            {
                dedicatedCoresPerFamilies = batchQuota.QuotaValues.DedicatedCoreQuotaPerVmFamily
                    .Select(r => new BatchVmCoresPerFamily(r.Key, r.Value))
                    .ToList();
            }

            return new BatchVmCoreQuota(numberOfCores,
                lowPriority,
                isDedicatedAndPerVmFamilyCoreQuotaEnforced,
                dedicatedCoresPerFamilies,
                new AccountQuota(batchQuota.QuotaValues.ActiveJobAndJobScheduleQuota,
                    batchQuota.QuotaValues.PoolQuota,
                    batchQuota.QuotaValues.DedicatedCoreQuota,
                    batchQuota.QuotaValues.LowPriorityCoreQuota));
        }

        private async Task<QuotaApiResponse> GetBatchAccountQuotaFromTerraAsync()
        {
            var batchResourceId = await GetBatchAccountResourceIdFromLandingZone();

            return await terraLandingZoneClient.GetResourceQuotaAsync(landingZoneId, batchResourceId, cacheResults: true);
        }

        private async Task<string> GetBatchAccountResourceIdFromLandingZone()
        {
            var resources = await terraLandingZoneClient.GetLandingZoneResourcesAsync(landingZoneId);

            var sharedResources = resources.Resources.FirstOrDefault(r => r.Purpose.Equals(SharedResourcePurpose));

            if (sharedResources is null)
            {
                throw new InvalidOperationException(
                    $"The Terra landing zone: {landingZoneId} does not contain shared resources");
            }

            var batchResource = sharedResources.DeployedResources.FirstOrDefault(r =>
                r.ResourceType.Equals(BatchAccountResourceType, StringComparison.OrdinalIgnoreCase));

            if (batchResource is null)
            {
                throw new InvalidOperationException($"The Terra landing zone: {landingZoneId} does not contain a shared batch account");
            }

            return batchResource.ResourceId;
        }

        private BatchVmFamilyQuotas ToVmFamilyBatchAccountQuotas(QuotaApiResponse batchAccountQuotas, string vmFamily, bool lowPriority, int? coresRequirement)
        {

            var isDedicated = !lowPriority;
            var totalCoreQuota = isDedicated ? batchAccountQuotas.QuotaValues.DedicatedCoreQuota : batchAccountQuotas.QuotaValues.LowPriorityCoreQuota;
            var isDedicatedAndPerVmFamilyCoreQuotaEnforced =
                isDedicated && batchAccountQuotas.QuotaValues.DedicatedCoreQuotaPerVmFamilyEnforced;

            var vmFamilyCoreQuota = isDedicatedAndPerVmFamilyCoreQuotaEnforced
                ? batchAccountQuotas.QuotaValues.DedicatedCoreQuotaPerVmFamily.FirstOrDefault(q => q.Key.Equals(vmFamily,
                          StringComparison.OrdinalIgnoreCase))
                      .Value
                : coresRequirement ?? 0;

            return new BatchVmFamilyQuotas(totalCoreQuota, vmFamilyCoreQuota, batchAccountQuotas.QuotaValues.PoolQuota,
                batchAccountQuotas.QuotaValues.ActiveJobAndJobScheduleQuota, batchAccountQuotas.QuotaValues.DedicatedCoreQuotaPerVmFamilyEnforced, vmFamily);
        }

    }
}
