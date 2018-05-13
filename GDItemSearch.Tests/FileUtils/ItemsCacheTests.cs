﻿using System;
using System.Diagnostics;
using System.IO;
using GDItemSearch.FileUtils.DBFiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GDItemSearch.Tests.FileUtils
{
    [TestClass]
    public class ItemsCacheTests
    {
        [TestMethod]
        public void TestLoadAllItemsFromCache()
        {
            ItemCache.Instance.CacheFilename = "Resources\\ItemsCache.json";

            ItemCache.Instance.LoadAllItems(null);

            var item = ItemCache.Instance.GetItem("records/items/lootsets/itemset_d017.dbr");
            Assert.AreEqual("records/skills/itemskills/legendary/item_ultoswrath.dbr", item.StringParametersRaw["itemSkillName"]);
        }
    }
}