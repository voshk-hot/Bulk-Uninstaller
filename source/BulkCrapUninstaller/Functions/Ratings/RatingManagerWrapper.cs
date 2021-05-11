/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using BrightIdeasSoftware;
using BulkCrapUninstaller.Forms;
using BulkCrapUninstaller.Properties;
using Klocman.Binding.Settings;
using Klocman.Forms.Tools;
using Klocman.Resources;
using Klocman.Tools;
using UninstallTools;

namespace BulkCrapUninstaller.Functions.Ratings
{
    internal class RatingManagerWrapper : IDisposable
    {
        private static readonly string RatingCacheFilename =
            Path.Combine(Program.AssemblyLocation.FullName, "RatingCashe.xml");

        private readonly UninstallerRatingManager _ratingManager
            = new UninstallerRatingManager(WindowsTools.GetUniqueUserId());

        private readonly SettingBinder<Settings> _settings = Settings.Default.SettingBinder;

        public void Dispose()
        {
            _ratingManager.Dispose();
        }

        /// <summary>
        /// Upload or discard user ratings based on current settings.
        /// </summary>
        public void ProcessGatheredRatings()
        {
            if (_settings.Settings.MiscUserRatings)
            {
                new Thread(() =>
                {
                    try
                    {
                        _ratingManager.UploadRatings();
                    }
                    catch
                    {
                        //TODO: Handle this better?
                    }
                    try
                    {
                        _ratingManager.SerializeCache(RatingCacheFilename);
                    }
                    catch
                    {
                        FlushRatings();
                    }
                    _ratingManager.Dispose();
                })
                { IsBackground = false, Name = "ProcessRatingDispose_Thread" }.Start();
            }
            else
            {
                FlushRatings();
            }
        }

        public void InitializeRatingColumn(OLVColumn olvColumnRating, ObjectListView uninstallerObjectListView)
        {
            olvColumnRating.AspectGetter = x =>
            {
                var entry = x as ApplicationUninstallerEntry;
                return string.IsNullOrEmpty(entry?.RatingId)
                    ? RatingEntry.NotAvailable
                    : _ratingManager.GetRating(entry.RatingId);
            };

            olvColumnRating.Renderer = new RatingRenderer();

            olvColumnRating.GroupKeyGetter = x =>
            {
                var model = x as ApplicationUninstallerEntry;

                if (!_settings.Settings.MiscUserRatings
                    || string.IsNullOrEmpty(model?.RatingId)
                    || _ratingManager.RatingCount <= 0)
                    return Localisable.NotAvailable;

                var rating = _ratingManager.GetRating(model.RatingId);

                if (rating.IsEmpty || (!rating.AverageRating.HasValue && !rating.MyRating.HasValue))
                    return CommonStrings.Unknown;

                return (rating.MyRating.HasValue ? "Your rating:" : "Average rating:") + " " +
                       RatingEntry.ToRating(rating.MyRating ?? (int) rating.AverageRating);
            };

            uninstallerObjectListView.CellClick += (x, y) =>
            {
                if (y.Column == null || (y.ModifierKeys != Keys.None) || !y.Column.Equals(olvColumnRating))
                    return;

                if (y.Model is not ApplicationUninstallerEntry model)
                    return;

                RateEntries(new[] {model}, uninstallerObjectListView.PointToScreen(y.Location));
            };
        }

        public void RateEntries(ApplicationUninstallerEntry[] entries, Point location)
        {
            if (!_settings.Settings.MiscUserRatings)
            {
                MessageBoxes.RatingsDisabled();
            }
            else if (!entries.Any() || entries.All(x => string.IsNullOrEmpty(x.RatingId)))
            {
                MessageBoxes.RatingUnavailable();
            }
            else
            {
                var title = entries.Length == 1
                    ? entries[0].DisplayName
                    : string.Format(CultureInfo.CurrentCulture, Localisable.RateTitle_Counted, entries.Length);

                var result = RatingPopup.ShowRateDialog(MessageBoxes.DefaultOwner, title, location);

                if (result == UninstallerRating.Unknown)
                    return;

                foreach (var entry in entries.Where(x => !string.IsNullOrEmpty(x.RatingId)))
                {
                    _ratingManager.SetMyRating(entry.RatingId, result);
                }
            }
        }

        public void InitializeRatings()
        {
            if (_settings.Settings.MiscUserRatings)
            {
                new Thread(() =>
                {
                    try
                    {
                        _ratingManager.DeserializeCache(RatingCacheFilename);
                    }
                    catch (Exception ex)
                    {
                        FlushRatings();
                        PremadeDialogs.GenericError(ex);
                    }

                    // If _ratingManager has no ratings it means that deserialization failed so we need to fetch from db
                    // Otherwise fetch at most every few hours, unless user manually clears the cache
                    if (_ratingManager.RatingCount > 0 && 
                        (DateTime.Now - _settings.Settings.MiscRatingCacheDate).Duration() < _settings.Settings._CacheUpdateRate)
                        return;

                    if (!WindowsTools.IsNetworkAvailable())
                        return;

                    try
                    {
                        _ratingManager.FetchRatings();
                        _settings.Settings.MiscRatingCacheDate = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                })
                { IsBackground = false, Name = "ProcessRatingInit_Thread" }.Start();
            }
            else
            {
                FlushRatings();
            }
        }

        private void FlushRatings()
        {
            try
            {
                _ratingManager.ClearRatings();
                UninstallerRatingManager.DeleteCache(RatingCacheFilename);
            }
            catch
            {
                //Ignore errors, the cashe won't be accessed anyways
            }

            _settings.Settings.MiscRatingCacheDate = DateTime.MinValue;
        }
    }
}