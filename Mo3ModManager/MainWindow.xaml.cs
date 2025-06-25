using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging; // Добавлен для работы с BitmapImage
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO; // Добавлен для работы с файловой системой
using System.Diagnostics; // Добавлен для System.Diagnostics.Process

namespace Mo3ModManager
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        NodeTree NodeTree;
        private string _currentModImagePath; // Добавлено поле для хранения пути к текущему изображению

        public MainWindow()
        {
            this.InitializeComponent();

            this.Title += " v" + System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            try
            {
                this.BuildTreeView();
                this.BuildProfiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private bool isCloseButtonEnabled = true;
        public bool IsCloseButtonEnabled
        {
            get
            {
                return this.isCloseButtonEnabled;
            }
            set
            {
                this.isCloseButtonEnabled = value;
                var hWnd = new System.Windows.Interop.WindowInteropHelper(this);
                var sysMenu = Win32.NativeMethods.GetSystemMenu(hWnd.Handle, false);
                Win32.NativeMethods.EnableMenuItem(sysMenu, Win32.NativeConstants.SC_CLOSE,
                    Win32.NativeConstants.MF_BYCOMMAND | (value ? Win32.NativeConstants.MF_ENABLED : Win32.NativeConstants.MF_GRAYED)
                    );
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/YoVVassup/mo3-mod-manager");
        }

        private void BuildProfiles()
        {
            this.ProfilesListView.Items.Clear();

            System.IO.DirectoryInfo[] profilesFolders = new System.IO.DirectoryInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles")).GetDirectories();
            foreach (var profileFolder in profilesFolders)
            {
                this.ProfilesListView.Items.Add(new ProfileItem(profileFolder));
            }
        }


        private void BuildTreeView()
        {
            this.ModTreeView.Items.Clear();

            this.NodeTree = new NodeTree();
            this.NodeTree.AddNodes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods"));

            foreach (var rootNode in this.NodeTree.RootNodes)
            {
                this.ModTreeView.Items.Add(new ModItem(rootNode));
            }
        }

        private void UpdateRunButtonStatus()
        {
            this.RunButton.IsEnabled = (this.ModTreeView.SelectedItem != null) && (this.ModTreeView.SelectedItem as ModItem).Node.IsRunnable && (this.ProfilesListView.SelectedItem != null);
        }

        private void ModTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.ModTreeView.SelectedItem != null)
            {
                var selectedItem = this.ModTreeView.SelectedItem as ModItem;
                this.ModsGroupBox.Header = "Мод: " + selectedItem.Title;

                // Установка описания мода
                if (ModDescriptionTextBlock != null) // Проверяем, что элемент XAML существует
                {
                    // Предполагается, что Node имеет свойство Description
                    // Если Node.Description может быть null, используем String.Empty или значение по умолчанию
                    ModDescriptionTextBlock.Text = selectedItem.Node.Description ?? String.Empty;
                }

                // Загрузка изображения мода
                if (ModImageViewer != null) // Проверяем, что элемент XAML существует
                {
                    // Путь к файлу изображения мода формируется как Mods/'Выбранный в TreeView мод'/image.png
                    // Предполагается, что selectedItem.Node.Directory содержит полный путь к директории выбранного мода.
                    string imagePath = System.IO.Path.Combine(selectedItem.Node.Directory, "image.png");
                    _currentModImagePath = imagePath; // Сохраняем путь к изображению

                    if (System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            // Создаем BitmapImage из файла изображения
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(imagePath);
                            // Использование CacheOption.OnLoad позволяет закрыть файл сразу после загрузки изображения
                            bitmap.CacheOption = BitmapCacheOption.OnLoad; 
                            bitmap.EndInit();
                            ModImageViewer.Source = bitmap;
                        }
                        catch (Exception ex)
                        {
                            // Обработка ошибок загрузки изображения
                            System.Diagnostics.Trace.WriteLine($"[Ошибка] Не удалось загрузить изображение: {ex.Message}");
                            ModImageViewer.Source = null; // Очищаем изображение при ошибке
                        }
                    }
                    else
                    {
                        ModImageViewer.Source = null; // Очищаем изображение, если файл не найден
                        // Опционально: можно установить изображение-заполнитель
                        // ModImageViewer.Source = new BitmapImage(new Uri("pack://application:,,,/YourAppName;component/Images/placeholder.png"));
                    }
                }

                this.DeleteModButton.IsEnabled = (selectedItem.Items.Count == 0);
            }
            else
            {
                this.ModsGroupBox.Header = "Моды:";

                // Очищаем описание и изображение, если мод не выбран
                if (ModDescriptionTextBlock != null)
                {
                    ModDescriptionTextBlock.Text = "Выберите мод, чтобы увидеть его описание и изображение.";
                }
                if (ModImageViewer != null)
                {
                    ModImageViewer.Source = null;
                }
                _currentModImagePath = null; // Очищаем путь к изображению

                this.DeleteModButton.IsEnabled = false;
            }
            this.UpdateRunButtonStatus();
        }

        // Новый метод для открытия изображения по клику
        private void ModImageViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentModImagePath) && File.Exists(_currentModImagePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_currentModImagePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть изображение: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void On_ProgProfilesListView_SelectionChanged()
        {
            if (this.ProfilesListView.SelectedItem != null)
            {
                var selectedItem = this.ProfilesListView.SelectedItem as ProfileItem;
                this.ProfilesGroupBox.Header = "Профиль: " + selectedItem.Name;

                this.RenameProfileButton.IsEnabled = true;
                this.DeleteProfileButton.IsEnabled = true;
            }
            else
            {
                this.ProfilesGroupBox.Header = "Профили:";

                this.RenameProfileButton.IsEnabled = false;
                this.DeleteProfileButton.IsEnabled = false;
            }
            this.UpdateRunButtonStatus();
        }

        private void ProfilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.On_ProgProfilesListView_SelectionChanged();
        }
        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.Assert((this.ModTreeView.SelectedItem as ModItem).Node.IsRunnable);

            var arguments = new ModProcessManagerArguments
            {
                Node = (this.ModTreeView.SelectedItem as ModItem).Node,
                // Случайный каталог отключен из-за настроек брандмауэра
                // RunningDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game-" + Guid.NewGuid().ToString().Substring(0, 8)),

                RunningDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game"),
                //ProfileDirectory = (this.ProfilesListView.SelectedItem as ProfileItem).Directory
                ProfileDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles", (this.ProfilesListView.SelectedItem as ProfileItem).Name, (this.ModTreeView.SelectedItem as ModItem).Node.ID)

            };


            this.IsEnabled = false;
            this.IsCloseButtonEnabled = false;

            ModProcessManager modProcessManager = new ModProcessManager(arguments);
            modProcessManager.RunWorkerCompleted += (object worker_sender, System.ComponentModel.RunWorkerCompletedEventArgs worker_e) =>
            {
                this.IsEnabled = true;
                this.IsCloseButtonEnabled = true;

                if (worker_e.Error == null)
                {
                    System.Diagnostics.Trace.WriteLine("[Примечание] Игра завершилась нормально. Все в порядке.");
                }
                else

                {
                    System.Diagnostics.Trace.WriteLine("[Ошибка] " + worker_e.Error.Message);
                    MessageBox.Show(worker_e.Error.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                this.BuildProfiles();
            };


            // Обходной путь для ОС <= Win7
            if (System.Environment.OSVersion.Version.Major <= 5 || System.Environment.OSVersion.Version.Major == 6 && System.Environment.OSVersion.Version.Minor <= 1)
            {
                modProcessManager.RunLegacyAsync(this);
            }
            else
            {
                // ОС >= Win8
                modProcessManager.RunAsync();
            }

        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(AppDomain.CurrentDomain.BaseDirectory);
        }

        private string PurifyFileName(string Filename)
        {
            // Заменить недопустимые символы
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                Filename = Filename.Replace(c.ToString(), "_");
            }
            return Filename;
        }

        private void NewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string newProfileName = InputWindow.ShowDialog(this, "Как будет называться новый профиль?", "Новый профиль...");
            newProfileName = this.PurifyFileName(newProfileName);

            if (String.IsNullOrWhiteSpace(newProfileName)) return;

            string newProfilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles", newProfileName);
            try
            {
                if (System.IO.Directory.Exists(newProfilePath))
                {
                    MessageBox.Show("Profiles\"" + newProfileName + "\" уже существует. Попробуйте другое имя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                var directoryInfo = System.IO.Directory.CreateDirectory(newProfilePath);
                this.ProfilesListView.Items.Add(new ProfileItem(directoryInfo));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.Assert(this.ProfilesListView.SelectedItem != null);
            var selectedItem = (this.ProfilesListView.SelectedItem as ProfileItem);


            string newProfileName = InputWindow.ShowDialog(this, "Как будет называться новый профиль?", "Новый профиль...");
            newProfileName = this.PurifyFileName(newProfileName);

            if (String.IsNullOrWhiteSpace(newProfileName)) return;

            string newProfilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles", newProfileName);

            try
            {
                if (System.IO.Directory.Exists(newProfilePath))
                {
                    MessageBox.Show("Profiles\"" + newProfileName + "\" уже существует. Попробуйте другое имя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                System.IO.Directory.Move(selectedItem.Directory, newProfilePath);
                selectedItem.ReplaceFrom(new ProfileItem(new System.IO.DirectoryInfo(newProfilePath)));
                this.On_ProgProfilesListView_SelectionChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.Assert(this.ProfilesListView.SelectedItem != null);
            var selectedItem = this.ProfilesListView.SelectedItem as ProfileItem;
            if (MessageBox.Show("Вы уверены, что хотите удалить профиль \"" + selectedItem.Name + "\"?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                try
                {
                    System.IO.Directory.Delete(selectedItem.Directory, true);
                    this.ProfilesListView.Items.Remove(selectedItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ProfilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.ProfilesListView.SelectedItem == null) return;
            var selectedItem = this.ProfilesListView.SelectedItem as ProfileItem;
            System.Diagnostics.Process.Start(selectedItem.Directory);
        }

        private void AboutButton_MouseEnter(object sender, MouseEventArgs e)
        {
            this.AboutButton.Content = new AccessText() { Text = "Автор: SadPencil, Мод: YoWassup" };
        }

        private void AboutButton_MouseLeave(object sender, MouseEventArgs e)
        {
            this.AboutButton.Content = new AccessText() { Text = "_О программе..." };
        }

        private void DeleteModButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = this.ModTreeView.SelectedItem as ModItem;
            if (MessageBox.Show("Вы уверены, что хотите удалить мод \"" + selectedItem.Name + "\"?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                try
                {
                    System.IO.Directory.Delete(selectedItem.Node.Directory, true);

                    this.NodeTree.RemoveNode(selectedItem.Node);

                    // После удаления узла из NodeTree, перестроим TreeView,
                    // чтобы обновить его визуальное представление.
                    this.BuildTreeView();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        private void InstallModButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Архив мода (*.zip)|*.zip",
                Title = "Установить мод..."
            };
            if ((bool)openFileDialog.ShowDialog())
            {
                try
                {
                    NodeTree testTree = new NodeTree(this.NodeTree);

                    // Примечание: элементы не копируются глубоко, будьте осторожны!
                    // Если изменить свойство в testTree, оно может изменить и в this.NodeTree!
                    var fastZip = new ICSharpCode.SharpZipLib.Zip.FastZip();

                    // Всегда перезаписывать, если целевые файлы уже существуют
                    fastZip.ExtractZip(
                        openFileDialog.FileName,
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Incoming"),
                        String.Empty);

                    testTree.AddNodes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Incoming"));

                    if (testTree.Count() == this.NodeTree.Count()) throw new Exception("Этот архив не содержит новых узлов.");

                    IO.CreateHardLinksOfFiles(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Incoming"), System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods"));

                    this.BuildTreeView();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IO.ClearDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Incoming"));
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!this.IsEnabled)
            {
                var result = System.Windows.MessageBox.Show(this, "Закрывайте менеджер модов только после выхода из игры, иначе вы можете потерять игровые данные. Нажмите \"Да\", если вы хотите выйти сейчас.", "Предупреждение",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Exclamation);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}