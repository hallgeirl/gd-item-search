﻿using GrimSearch.Common;
using GrimSearch.Utils;
using GrimSearch.Utils.DBFiles;
using Microsoft.Win32;
using Newtonsoft.Json;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace GrimSearch.ViewModels
{
    class MainViewModel : ViewModelBase
    {
        public MainViewModel(Dispatcher dispatcher) : this()
        {
            Dispatcher = dispatcher;
        }

        public MainViewModel()
        {
            SearchCommand = new DelegateCommand(async () => 
            {
                await SearchAsync();
            });

            SaveSettingsCommand = new DelegateCommand(async () =>
            {
                await SaveSettingsAsync();
            });

            ClearCacheCommand = new DelegateCommand(() =>
            {
                ClearCache();
            });

            RefreshCommand = new DelegateCommand(() =>
            {
                Refresh();
            });
            DetectGDSettingsCommand = new DelegateCommand(async () =>
            {
                await TryDetectGDSettings();
            });
        }

        public async Task Initialize()
        {
            await LoadSettingsAsync();
        }

        bool _initialized = false;
        private Index _index = new Index();
        const string settingsFile = "GDItemSearchSettings.json";

        #region Events

        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured;

        private void FireErrorOccured(string text, Exception exception)
        {
            ErrorOccured?.Invoke(this, new ErrorOccuredEventArgs() { ErrorMessage = text, Exception = exception });
        }

        public event EventHandler SettingsMissing;

        #endregion

        #region UI properties
        private string _statusBarText = "";
        public string StatusBarText
        {
            get
            {
                return _statusBarText;
            }
            set
            {
                _statusBarText = value;
                RaisePropertyChangedEvent("StatusBarText");
            }
        }

        public Dispatcher Dispatcher;

        private bool _autoRefresh = true;
        public bool AutoRefresh
        {
            get { return _autoRefresh; }
            set { _autoRefresh = value; RaisePropertyChangedEvent("AutoRefresh"); }
        }

        private bool _enableInput = true;
        public bool EnableInput
        {
            get { return _enableInput; }
            set { _enableInput = value; RaisePropertyChangedEvent("EnableInput"); }
        }

        private ObservableCollection<string> _searchModes = new ObservableCollection<string> { "Regular", "Duplicate search" };
        public ObservableCollection<string> SearchModes
        {
            get { return _searchModes; }
            set { _searchModes = value; RaisePropertyChangedEvent("SearchModes"); }
        }


        private ObservableCollection<MultiselectComboItem> _itemTypes = new ObservableCollection<MultiselectComboItem>();
        public ObservableCollection<MultiselectComboItem> ItemTypes
        {
            get { return _itemTypes; }
            set { _itemTypes = value; RaisePropertyChangedEvent("ItemTypes"); }
        }


        private ObservableCollection<MultiselectComboItem> _itemQualities;
        public ObservableCollection<MultiselectComboItem> ItemQualities
        {
            get { return _itemQualities; }
            set { _itemQualities = value; RaisePropertyChangedEvent("ItemQualities"); }
        }


        private ObservableCollection<ItemViewModel> _searchResults = new ObservableCollection<ItemViewModel>();
        public ObservableCollection<ItemViewModel> SearchResults
        {
            get { return _searchResults; }
            set { _searchResults = value; RaisePropertyChangedEvent("SearchResults"); }
        }

        #endregion

        #region Search filters

        private string _searchMode = "Regular";
        public string SearchMode
        {
            get { return _searchMode; }
            set
            {
                _searchMode = value;
                RaisePropertyChangedEvent("SearchMode");
            }
        }

        private int _minimumLevel = 0;
        public int MinimumLevel
        {
            get { return _minimumLevel; }
            set
            {
                _minimumLevel = value;
                RaisePropertyChangedEvent("MinimumLevel");
            }
        }


        private int _maximumLevel = 100;
        public int MaximumLevel
        {
            get { return _maximumLevel; }
            set
            {
                _maximumLevel = value;
                RaisePropertyChangedEvent("MaximumLevel");
            }
        }


        private bool _showEquipped;
        public bool ShowEquipped
        {
            get { return _showEquipped; }
            set
            {
                _showEquipped = value;
                RaisePropertyChangedEvent("ShowEquipped");
            }
        }

        private string _searchString;
        public string SearchString
        {
            get
            {
                return _searchString;
            }
            set
            {
                _searchString = value;
                RaisePropertyChangedEvent("SearchString");
            }
        }

        #endregion

        #region Settings


        private string _grimDawnDirectory;
        public string GrimDawnDirectory
        {
            get { return _grimDawnDirectory; }
            set { _grimDawnDirectory = value; RaisePropertyChangedEvent("GrimDawnDirectory"); }
        }


        private FileSystemWatcher _savesWatcher = null;
        private string _grimDawnSavesDirectory;
        public string GrimDawnSavesDirectory
        {
            get { return _grimDawnSavesDirectory; }
            set
            {
                _grimDawnSavesDirectory = value;
                RaisePropertyChangedEvent("GrimDawnSavesDirectory");
                WatchDirectory(value);
            }
        }

        private void WatchDirectory(string value)
        {
            if (_savesWatcher != null)
                _savesWatcher.Dispose();

            if (!Directory.Exists(value))
                return;

            _savesWatcher = new FileSystemWatcher(value);
            _savesWatcher.IncludeSubdirectories = true;
            _savesWatcher.EnableRaisingEvents = true;
            _savesWatcher.Changed += _savesWatcher_Changed;
        }

        private void _savesWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (Dispatcher == null || !AutoRefresh)
                return;

            Dispatcher.Invoke(() =>
            {
                Refresh();
            });
        }

        #endregion

        #region commands

        public ICommand SaveSettingsCommand { get; set; }
        public ICommand ClearCacheCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand SearchAndSaveCommand { get; set; }
        public ICommand RefreshCommand { get; set; }
        public ICommand DetectGDSettingsCommand { get; set; }



        #endregion

        #region Settings
        public async Task SaveSettingsAsync(bool skipIndexBuild = false)
        {
            var storedSettings = new StoredSettings()
            {
                GrimDawnDirectory = GrimDawnDirectory,
                SavesDirectory = GrimDawnSavesDirectory,
                AutoRefresh = AutoRefresh,
                LastSearchMode = SearchMode,
                LastSearchText = SearchString
            };
            try
            {
                StatusBarText = "Saving settings...";
                EnableInput = false;
                File.WriteAllText(settingsFile, JsonConvert.SerializeObject(storedSettings));

                if (!skipIndexBuild)
                    await BuildIndexAsync();

                _initialized = true;
            }
            catch (Exception ex)
            {
                FireErrorOccured("An error occured while saving settings.", ex);
            }
            finally
            {
                ResetStatusBarText();
                EnableInput = true;
            }
        }

        private async Task LoadSettingsAsync()
        {
            if (File.Exists(settingsFile))
            {
                try
                {
                    StatusBarText = "Loading settings...";
                    var settings = JsonConvert.DeserializeObject<StoredSettings>(File.ReadAllText(settingsFile));
                    GrimDawnDirectory = settings.GrimDawnDirectory;
                    GrimDawnSavesDirectory = settings.SavesDirectory;
                    AutoRefresh = settings.AutoRefresh;
                    SearchMode = settings.LastSearchMode;
                    SearchString = settings.LastSearchText;

                    await BuildIndexAsync();

                    _initialized = true;

                    await SearchAsync();
                }
                catch (Exception ex)
                {
                    FireErrorOccured("An error occured while loading settings.", ex);
                }
                finally
                {
                    ResetStatusBarText();
                }

            }
            else
            {
                GrimDawnDirectory = "";
                GrimDawnSavesDirectory = "";

                SettingsMissing?.Invoke(this, new EventArgs());
            }
        }


        private void ClearCache()
        {
            SetStatusbarText("Clearing cache...");
            _index.ClearCache();
            ResetStatusBarText();
        }

        private void ResetStatusBarText()
        {
            SetStatusbarText("Ready");
        }

        private void SetStatusbarText(string text)
        {
            if (Dispatcher == null)
                return;
            Dispatcher.Invoke(() =>
            {
                StatusBarText = text;
            });
        }

        private async Task TryDetectGDSettings()
        {
            string steamPath = GetRegistryValue<string>("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath");
            int activeUser = GetRegistryValue<int>("HKEY_CURRENT_USER\\Software\\Valve\\Steam\\ActiveProcess", "ActiveUser");

            if (!Directory.Exists(steamPath))
                throw new InvalidOperationException("Steam path was not found. Is it installed?");

            if (activeUser == 0)
                throw new InvalidOperationException("Steam is not running, or you are not logged in.");

            string errors = "";

            string gdDir = GetInstallLocation(steamPath);
            string savesDir = System.IO.Path.Combine(steamPath, "userdata", activeUser.ToString(), "219990", "remote", "save").Replace('/', '\\');

            if (!File.Exists(System.IO.Path.Combine(gdDir, "ArchiveTool.exe")))
            {
                errors += "The Grim Dawn directory was not found in the default install location for Steam games. Please specify this manually.";
            }
            else
            {
                GrimDawnDirectory = gdDir;
            }

            
            if (!Directory.Exists(System.IO.Path.Combine(savesDir, "main")))
            {
                errors += "Grim Dawn saves directory was not found at " + savesDir + ". Please specify this manually.";
            }
            else
            {
                GrimDawnSavesDirectory = savesDir;
            }

            if (!string.IsNullOrEmpty(errors))
                throw new InvalidOperationException(errors);
            else
            {
                await Dispatcher.Invoke(async () =>
                {
                    var answer = MessageBox.Show("The Grim Dawn directories have been successfully detected. Do you want to save and start loading items and characters?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Yes)
                        await SaveSettingsAsync();
                });
            }
        }

        private string[] GetAllPossibleInstallLocations(string steamPath)
        {
            List<string> locations = new List<string>();
            locations.Add(Path.Combine(steamPath, "SteamApps", "common", "Grim Dawn").Replace('/', '\\'));
            locations.AddRange(GetInstallLocationsFromSteamConfig(steamPath));
            

            return locations.ToArray();
        }

        private string[] GetInstallLocationsFromSteamConfig(string steamPath)
        {
            var configPath = Path.Combine(steamPath, "config", "config.vdf");
            if (!File.Exists(configPath))
                return new string[0];
            var configContent = File.ReadAllText(configPath);
            
            var configJson = VdfFileReader.ToJson(configContent);

            var deserialized = JsonConvert.DeserializeObject<SteamConfig>(configJson);

            var steamConfigInstallKeys = deserialized.Software.Valve.Steam.Keys.Where(x => x.StartsWith("BaseInstallFolder_"));

            List<string> results = new List<string>();

            foreach (var k in steamConfigInstallKeys)
            {
                var val = deserialized.Software.Valve.Steam[k] as string;
                if (string.IsNullOrEmpty(val))
                    continue;
                var fullGDPath = Path.Combine(val, "SteamApps", "common", "Grim Dawn").Replace('/', '\\');
                results.Add(fullGDPath);
            }
                
            return results.ToArray();
        }

        private string GetInstallLocation(string steamPath)
        {
            var allLocations = GetAllPossibleInstallLocations(steamPath);
            foreach (var l in allLocations)
            {
                if (File.Exists(System.IO.Path.Combine(l, "ArchiveTool.exe")))
                    return l;
            }

            return null;
        }

        private T GetRegistryValue<T>(string path, string valueName)
        {
            var value = Registry.GetValue(path, valueName, null);

            if (value == null)
                return default(T);

            return (T)value;
        }

        #endregion

        #region Search and index

        private async Task BuildIndexAsync(bool skipItemTypesReload = false)
        {
            SetStatusbarText("Loading characters and items...");
            IndexSummary result;

            var newIndex = new Index();
            result = await newIndex.BuildAsync(GrimDawnDirectory, GrimDawnSavesDirectory, (msg) => SetStatusbarText(msg)).ConfigureAwait(false);
            _index = newIndex;

            if (!skipItemTypesReload)
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    var itemQualities = new ObservableCollection<MultiselectComboItem>();
                    itemQualities.AddRange(result.ItemRarities.Select(x => new MultiselectComboItem() { Selected = (x != "Common" && x != "Rare" && x != "Magical"), Value = x, DisplayText = x }));
                    ItemQualities = itemQualities;

                    ItemTypes.Clear();
                    ItemTypes.AddRange(result.ItemTypes.Select(x => new MultiselectComboItem() { Selected = true, Value = x, DisplayText = ItemHelper.GetItemTypeDisplayName(x) }));
                }));
            }
        }


        private async Task SearchAsync()
        {
            if (!_initialized)
                return;

            var filter = CreateIndexFilter();
            SetStatusbarText("Searching for " + SearchString);

            List<IndexItem> items;

            if (SearchMode == "Duplicate search")
                items = await _index.FindDuplicatesAsync(SearchString, filter).ConfigureAwait(false);
            else
                items = await _index.FindAsync(SearchString, filter).ConfigureAwait(false);
            
            Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
                SearchResults.AddRange(items.Select(x => ItemViewModel.FromModel(x)));
            });

            ResetStatusBarText();
        }

        private IndexFilter CreateIndexFilter()
        {
            IndexFilter filter = new IndexFilter();
            filter.MinLevel = MinimumLevel;
            filter.MaxLevel = MaximumLevel;

            filter.IncludeEquipped = ShowEquipped;

            var rarityItems = ItemQualities;
            if (rarityItems != null)
                filter.ItemQualities = rarityItems.Where(x => x.Selected).Select(x => x.Value).ToArray();

            var itemTypes = ItemTypes as IEnumerable<MultiselectComboItem>;
            if (itemTypes != null)
                filter.ItemTypes = itemTypes.Where(x => x.Selected).Select(x => x.Value).ToArray();

            filter.PageSize = 50;
            return filter;
        }

        private async void Refresh()
        {
            SetStatusbarText("Refreshing...");
            await BuildIndexAsync(true).ConfigureAwait(false);
            await SearchAsync().ConfigureAwait(false);
            ResetStatusBarText();
        }
        #endregion
    }
}
