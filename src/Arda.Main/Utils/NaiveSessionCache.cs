﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Extensions.Caching.Distributed;

namespace Arda.Main.Utils
{
    public enum NaiveSessionCacheResource
    {
        MicrosoftGraph = 1,
        PowerBi = 2
    }

    public class NaiveSessionCache : TokenCache
    {
        private static readonly object FileLock = new object();
        string UserObjectId = string.Empty;
        string CacheId = string.Empty;
        IDistributedCache Cache = null;

        public NaiveSessionCache(string userId, NaiveSessionCacheResource resource, IDistributedCache cache)
        {
            UserObjectId = userId;
            CacheId = $"{UserObjectId}_{resource.ToString()}_TokenCache";
            Cache = cache;
            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            Load();
        }

        public void Load()
        {
            lock (FileLock)
            {
                this.Deserialize(Cache.Get(CacheId));
            }
        }

        public void Persist()
        {
            lock (FileLock)
            {
                // reflect changes in the persistent store
                Cache.Set(CacheId, this.Serialize());
                // once the write operation took place, restore the HasStateChanged bit to false
                this.HasStateChanged = false;
            }
        }

        // Empties the persistent store.
        public override void Clear()
        {
            base.Clear();
            Cache.Remove(CacheId);
        }

        public override void DeleteItem(TokenCacheItem item)
        {
            base.DeleteItem(item);
            Persist();
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            Load();
        }

        // Triggered right after ADAL accessed the cache.
        void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (this.HasStateChanged)
            {
                Persist();
            }
        }
    }
}