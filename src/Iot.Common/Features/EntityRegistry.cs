using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iot.Common
{
    public class EntityRegistry
    {
        private static ConcurrentDictionary<string, EntityConfig>    entitiesBag = new ConcurrentDictionary<string, EntityConfig>();


        public static bool GetEntityConfigFor(string entityName, out Type entityType, out long partition)
        {
            bool bRet = false;

            if (IsEntityAlreadyRegistered(entityName))
            {
                entitiesBag.TryGetValue(entityName, out EntityConfig config);

                entityType = config.EntityType;
                partition = config.Partition;
                bRet = true;
            }
            else
            {
                entityType = null;
                partition = 0;
            }

            return bRet;
        }

        public static bool GetEntityConfigFor(string entityName, out Type entityType)
        {
            bool bRet = false;

            if (IsEntityAlreadyRegistered(entityName))
            {
                entitiesBag.TryGetValue(entityName, out EntityConfig config);

                entityType = config.EntityType;
                bRet = true;
            }
            else
            {
                entityType = null;
            }

            return bRet;
        }

        public static bool GetEntityConfigFor( string entityName, out long partition )
        {
            bool bRet = false;

            if(IsEntityAlreadyRegistered(entityName))
            {
                entitiesBag.TryGetValue(entityName, out EntityConfig config);

                partition = config.Partition;
                bRet = true;
            }
            else
            {
                partition = 0;
            }

            return bRet;
        }


        public static bool IsEntityAlreadyRegistered(string entityName)
        {
            return entitiesBag.ContainsKey(entityName);
        }

        public static bool RegisterEntity( string entityName, Type entityType )
        {
            bool bRet = false;

            if( !IsEntityAlreadyRegistered(entityName))
            {
                entitiesBag.TryAdd(entityName, new EntityConfig(entityName, entityType));
                bRet = true;
            }

            return bRet;
        }

        public static bool UnRegisterEntity(string entityName )
        {
            bool bRet = false;

            if (IsEntityAlreadyRegistered(entityName))
            {
                entitiesBag.TryRemove(entityName, out EntityConfig config );
                bRet = true;
            }

            return bRet;
        }




        // PRIVATE CLASSES
        private class EntityConfig
        {
            public EntityConfig( string entityName, Type entityType )
            {
                this.EntityName = entityName.ToLower();
                this.Partition = FnvHash.Hash(this.EntityName);
                this.EntityType = entityType;
            }

            public string EntityName { get; private set; }
            
            public long Partition { get; private set; }

            public Type EntityType { get; private set; }
        }
    }
}
