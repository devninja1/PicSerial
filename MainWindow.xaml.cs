using Microsoft.Win32;

using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

using static System.Net.Mime.MediaTypeNames;

using Image = System.Windows.Controls.Image;

namespace PicSerial
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ImageItem> Images { get; set; } = new ObservableCollection<ImageItem>();
        private string selectedFolder = string.Empty;
        private int imageCount = 0;
        private Point _dragStartPoint;
        private bool _isClosing = false; // flag to avoid loop

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(800));
            BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosing) return; // already closing, skip

            e.Cancel = true; // stop immediate close
            _isClosing = true;

            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, _) =>
            {
                // force close without triggering Closing again
                System.Windows.Application.Current.Shutdown();
            };
            BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private async void BtnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedFolder = Path.GetDirectoryName(dialog.FileName);
                Images.Clear();
                imageCount = 0;
                UpdateStatus();

                // Show spinner + message
                StartSpinner();

                // Yield to UI so spinner/message actually render
                await Task.Delay(100);

                var existingFiles = Directory.GetFiles(selectedFolder, "*.*")
                                            .Where(IsImageFile)
                                            .OrderBy(f => f)
                                            .ToList();

                await Task.Run(() =>
                {
                    int processed = 0;
                    foreach (var file in existingFiles)
                    {
                        processed++;
                        string expectedName = $"{processed:D4}{Path.GetExtension(file)}";
                        string expectedPath = Path.Combine(selectedFolder, expectedName);

                        // If already correctly named, just use it
                        string finalPath = file;
                        if (Path.GetFileName(file) != expectedName)
                        {
                            // Rename instead of duplicating
                            File.Move(file, expectedPath);
                            finalPath = expectedPath;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            Images.Add(new ImageItem(finalPath, file));
                            imageCount = processed;
                            UpdateStatus();
                        });
                    }
                });


                StopSpinner();
                ShowToast("Folder images loaded successfully!");
            }
        }


        private void BtnClearPreviews_Click(object sender, RoutedEventArgs e)
        {
            Images.Clear();
            UpdateStatus();
            ShowToast("Previews cleared!");
        }

        private void BtnShowFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder))
                ShowToast("No folder selected yet.");
            else
                ShowToast("Current Folder: " + selectedFolder);
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder))
            {
                ShowToast("No folder selected yet.");
            }
            else
            {
                try
                {
                    Process.Start("explorer.exe", selectedFolder);
                }
                catch (Exception ex)
                {
                    ShowToast("Error opening folder: " + ex.Message);
                }
            }
        }

        private void DragArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = files.All(IsImageFile) ? DragDropEffects.Copy : DragDropEffects.None;
            }
        }

        private async void DragArea_Drop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder))
            {
                ShowToast("Please select a folder first.");
                return;
            }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return; // ✅ guard against null

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var imageFiles = files.Where(IsImageFile).ToList();

            if (imageFiles.Count == 0) return;

            StartSpinner("Copying images..."); // show spinner instead of progress bar
            ShowToast("Copying files...");

            await Task.Run(() =>
            {
                foreach (string file in imageFiles)
                {
                    imageCount++;
                    string newFileName = Path.Combine(selectedFolder, imageCount.ToString("D4") + Path.GetExtension(file));
                    File.Copy(file, newFileName, true);

                    Dispatcher.Invoke(() =>
                    {
                        Images.Add(new ImageItem(newFileName, file));
                        UpdateStatus();
                    });
                }
            });

            StopSpinner(); // hide spinner when done
            ShowToast("Images copied successfully!");
        }


        private bool IsImageFile(string file)
        {
            string ext = Path.GetExtension(file).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif";
        }
               

        private void UpdateStatus()
        {
            StatusFolder.Text = string.IsNullOrEmpty(selectedFolder)
                ? "No folder selected"
                : $"Folder: {selectedFolder}";
           
           // StatusCounter.Text = $"Images copied: {imageCount}";
            StatusCounter.Text = $"Images copied: {Images.Count}";
        }

        private void OpenNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ImageItem item)
                Process.Start("explorer.exe", item.FilePath);
        }

        private void OpenOriginalFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ImageItem item)
                Process.Start("explorer.exe", item.OriginalPath);
        }

        private void DeleteNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ImageItem item)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                        Images.Remove(item);
                        ShowToast($"Deleted: {Path.GetFileName(item.FilePath)}");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast("Error deleting file: " + ex.Message);
                }
            }
        }

        private void ThumbnailList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ThumbnailList_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var container = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (container == null) return;

                var draggedItem = (ImageItem)container.DataContext;
                DragDrop.DoDragDrop(container, draggedItem, DragDropEffects.Move);
            }
        }

        private void ThumbnailList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ImageItem)))
            {
                var droppedData = e.Data.GetData(typeof(ImageItem)) as ImageItem;
                var target = ((FrameworkElement)e.OriginalSource).DataContext as ImageItem;

                if (droppedData != null && target != null && droppedData != target)
                {
                    int oldIndex = Images.IndexOf(droppedData);
                    int newIndex = Images.IndexOf(target);

                    if (oldIndex >= 0 && newIndex >= 0)
                        Images.Move(oldIndex, newIndex);
                }
            }
        }

        // Helper
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private async void RenameSerial_Click(object sender, RoutedEventArgs e)
        {
            if (Images == null || Images.Count == 0)
            {
                ShowToast("No images to rename.");
                return;
            }

            string dir = Path.GetDirectoryName(Images.First().FilePath);
            int counter = 1;

            StartSpinner("Renaming images...");

            // Pass 1: temporary names
            foreach (var item in Images)
            {
                if (!File.Exists(item.FilePath))
                {
                    ShowToast($"File not found: {item.FilePath}");
                    continue;
                }

                string tempName = Path.Combine(dir, $"tmp_{counter:D4}{Path.GetExtension(item.FilePath)}");

                // Avoid collision with existing tmp file
                if (File.Exists(tempName))
                {
                    File.Delete(tempName);
                }

                File.Move(item.FilePath, tempName);
                item.FilePath = tempName;
                counter++;
            }

            // Pass 2: final serial names
            counter = 1;
            foreach (var item in Images)
            {
                if (!File.Exists(item.FilePath))
                {
                    ShowToast($"Temp file missing: {item.FilePath}");
                    continue;
                }

                string newName = Path.Combine(dir, $"{counter:D4}{Path.GetExtension(item.FilePath)}");

                // Avoid collision with existing final file
                if (File.Exists(newName))
                {
                    File.Delete(newName);
                }

                File.Move(item.FilePath, newName);
                item.FilePath = newName;
                item.DisplayName = Path.GetFileName(newName);

                await Task.Delay(100); // short delay to release locks
                counter++;
            }

            // ✅ Clear and reload thumbnails after renaming
            await Task.Delay(200); // ensure file system settles
            ReloadThumbnails(dir);
            UpdateStatus();
            StopSpinner();
            ShowToast("Renamed successfully!");
        }


        private void ReloadThumbnails(string folderPath)
        {
            Images.Clear();

            var files = Directory.GetFiles(folderPath)
                                 .Where(f => IsImageFile(f))
                                 .OrderBy(f => f);

            foreach (var file in files)
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file);
                bitmap.DecodePixelWidth = 100;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();

                Images.Add(new ImageItem(file, Path.GetFileName(file)) { Thumbnail = bitmap });
            }
        }


        private void StartSpinner(string message = "Loading images...")
        {
            LoadingText.Text = message;
            LoaderOverlay.Visibility = Visibility.Visible;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            LoaderOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };

            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
        }

        private void StopSpinner()
        {
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => LoaderOverlay.Visibility = Visibility.Collapsed;
            LoaderOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private async void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastMessage.Visibility = Visibility.Visible;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            ToastMessage.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Keep visible for 2 seconds
            await Task.Delay(2000);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fadeOut.Completed += (s, e) => ToastMessage.Visibility = Visibility.Collapsed;
            ToastMessage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }


    }

    public class ImageItem
    {
        public string FilePath { get; set; }
        public string OriginalPath { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public string DisplayName { get; set; }

        public ImageItem(string newFilePath, string originalFilePath)
        {
            FilePath = newFilePath;
            OriginalPath = originalFilePath;
            DisplayName = Path.GetFileName(newFilePath);

            Thumbnail = new BitmapImage();
            Thumbnail.BeginInit();
            Thumbnail.UriSource = new Uri(newFilePath);
            Thumbnail.DecodePixelWidth = 100; // lightweight thumbnail
            Thumbnail.CacheOption = BitmapCacheOption.OnLoad;
            Thumbnail.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            Thumbnail.EndInit();
            Thumbnail.Freeze();
        }
    }
}