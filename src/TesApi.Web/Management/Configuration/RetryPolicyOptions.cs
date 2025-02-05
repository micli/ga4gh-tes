﻿namespace TesApi.Web.Management.Configuration
{
    /// <summary>
    /// Retry policy options
    /// </summary>
    public class RetryPolicyOptions
    {
        /// <summary>
        /// Retry policy configuration section
        /// </summary>
        public const string RetryPolicy = "RetryPolicy";

        private const int DefaultRetryCount = 3;
        private const int DefaultExponentialBackOffExponent = 2;

        /// <summary>
        /// Max retry count
        /// </summary>
        public int MaxRetryCount { get; set; } = DefaultRetryCount;

        /// <summary>
        /// BackOff exponent
        /// </summary>
        public int ExponentialBackOffExponent { get; set; } = DefaultExponentialBackOffExponent;
    }
}
