using System.Collections.Generic;
using MediaBrowser.Common.Configuration;

namespace CnSub
{
    public static class ConfigurationExtension
    {
        public static CnSubOptions GetCnSubConfiguration(this IConfigurationManager manager)
        {
            return manager.GetConfiguration<CnSubOptions>("cn_sub");
        }
    }

    public class CnSubConfigurationFactory : IConfigurationFactory
    {
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new ConfigurationStore[]
            {
                new ConfigurationStore
                {
                    Key = "cn_sub",
                    ConfigurationType = typeof (CnSubOptions)
                }
            };
        }
    }

    public class CnSubOptions
    {
        public string SubhdUsername { get; set; }
        public string SubhdPasswordHash { get; set; }
    }
}
