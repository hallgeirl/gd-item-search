﻿using GDItemSearch.FileUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDItemSearch
{
    public class StoredSettings
    {
        public StoredSettings()
        {
            GrimDawnDirectory = Settings.GrimDawnDirectory;
            SavesDirectory = Settings.SavesDirectory;
        }

        public string GrimDawnDirectory { get; set; }
        public string SavesDirectory { get; set; }
    }
}