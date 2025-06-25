using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Mo3ModManager
{
    class ModProcessManager
    {
        public ModProcessManager(ModProcessManagerArguments Arguments)
        {
            this.ProfileDirectory = Arguments.ProfileDirectory;
            this.RunningDirectory = Arguments.RunningDirectory;
            this.Node = Arguments.Node;
        }


        /*
        /// <summary>
        /// Only make sense in multi-threading. Not implemented now.
        /// </summary>
        public enum ProcessStatus
        {
            Prepairing,
            Running,
            Cleaning,
            Finished
        };

        /// <summary>
        /// Only make sense in multi-threading. Not implemented now.
        /// </summary>
        public ProcessStatus Status { get; set; }
        */

        public string RunningDirectory { get; set; }
        public string ProfileDirectory { get; set; }

        //public ModItem ModItem { get; set; }
        public Node Node { get; set; }



        /// <summary>
        /// Удаляет символические ссылки как для узла, так и для его предков.
        /// Примечание: если файл был удален и перестроен, он будет рассматриваться как отдельный файл и будет сохранен.
        /// </summary>
        /// <param name="Node">Узел</param>
        private void CleanNode(Node Node,object addition =null)
        {
            // Источник и назначение совпадают с "PrepareNode"
            string sourceDirectory = Node.FilesDirectory;
            Debug.WriteLine(sourceDirectory);
            string destinationDirectory = this.RunningDirectory;

            // Удаление жесткой ссылки NTFS равносильно удалению файла
            Exception ex = null;
            if (addition != null)
            {
                ex = (Exception)addition;
            }
            foreach (string srcFullName in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativeName = srcFullName.Substring(sourceDirectory.Length + 1);
                string destFullName = Path.Combine(destinationDirectory, relativeName);
                if (File.Exists(destFullName))
                {
                    // Определяем, являются ли два файла на самом деле одинаковыми.
                    if (IO.IsSameFile(srcFullName, destFullName))
                    {
                        //Debug.WriteLine("Delete file: " + destFullName);
                        try
                        {
                            File.Delete(destFullName);
                        }
                        catch (Exception e)
                        {
                            ex = e;
                        }

                    }
                    else
                    {
                        Debug.WriteLine("Сохранено: " + relativeName);
                    }

                }
            }

            // Рекурсивно для родителя
            if (!Node.IsRoot)
            {
                this.CleanNode(Node.Parent,ex);
            }
            if (ex != null)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Создает символические ссылки как для узла, так и для его предков
        /// </summary>
        /// <param name="Node">Узел</param>
        private void PrepareNode(Node Node)
        {
            string srcDirectory = Node.FilesDirectory;
            string destDirectory = this.RunningDirectory;

            IO.CreateHardLinksOfFiles(srcDirectory, destDirectory, false, new List<string>() { ".INI" });

            // Рекурсивно для родителя
            if (!Node.IsRoot)
            {
                this.PrepareNode(Node.Parent);
            }
        }


        /// <summary>
        /// Запускает программу и ждет, пока все ее дочерние процессы завершатся. Даже если программа завершилась раньше своих дочерних процессов, этот метод все равно будет работать правильно.
        /// Поддерживается только для Windows 8 и новее.
        /// </summary>
        private void RunStep2_RunAndWait()
        {
            this.RunStep2_RunAndWait(Path.Combine(this.RunningDirectory, this.Node.MainExecutable), this.Node.Arguments, this.RunningDirectory);
        }

        /// <summary>
        /// Запускает программу и ждет, пока все ее дочерние процессы завершатся. Даже если программа завершилась раньше своих дочерних процессов, этот метод все равно будет работать правильно.
        /// Поддерживается только для Windows 8 и новее.
        /// </summary>
        /// <param name="Fullname">Путь к программе.</param>
        /// <param name="Arguments">Аргументы.</param>
        /// <param name="WorkingDirectory">Рабочий каталог.</param>
        private void RunStep2_RunAndWait(string Fullname, string Arguments, string WorkingDirectory)
        {
            string commandLine = '"' + Fullname + '"' + (String.IsNullOrEmpty(Arguments) ? string.Empty : ' ' + Arguments);

            // См.: https://blogs.msdn.microsoft.com/oldnewthing/20130405-00/?p=4743

            Trace.WriteLine("[Примечание] Запуск игры...");

            IntPtr jobHandle = Win32.NativeMethods.CreateJobObjectW(IntPtr.Zero, String.Empty);
            if (jobHandle == IntPtr.Zero)
            {
                throw new Exception("Ошибка WinAPI CreateJobObjectW. Код ошибки " + Win32.NativeMethods.GetLastError());
            }


            IntPtr ioPortHandle = Win32.NativeMethods.CreateIoCompletionPort(Win32.NativeConstants.INVALID_HANDLE_VALUE, IntPtr.Zero, 0, 1);
            if (ioPortHandle == IntPtr.Zero)
            {
                throw new Exception("Ошибка WinAPI CreateIoCompletionPort. Код ошибки " + Win32.NativeMethods.GetLastError());
            }

            var portStruct = new Win32.JOBOBJECT_ASSOCIATE_COMPLETION_PORT()
            {
                CompletionKey = jobHandle,
                CompletionPort = ioPortHandle
            };

            IntPtr portStructIntPtr = IntPtr.Zero;
            try
            {
                int portStructLength = Marshal.SizeOf(portStruct);
                portStructIntPtr = Marshal.AllocHGlobal(portStructLength);
                Marshal.StructureToPtr(portStruct, portStructIntPtr, false);
                if (!Win32.NativeMethods.SetInformationJobObject(
                    jobHandle,
                    Win32.JOBOBJECTINFOCLASS.JobObjectAssociateCompletionPortInformation,
                    portStructIntPtr,
                    (uint)portStructLength
                    ))
                {
                    throw new Exception("Ошибка WinAPI SetInformationJobObject. Код ошибки " + Win32.NativeMethods.GetLastError());
                }

            }
            finally
            {
                if (portStructIntPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(portStructIntPtr);
            }

            var processInformationStruct = new Win32.PROCESS_INFORMATION() { };

            var startupInfoStruct = new Win32.STARTUPINFOW() { };

            // Показать курсор "Работа в фоновом режиме"
            startupInfoStruct.dwFlags = Win32.NativeConstants.STARTF_FORCEONFEEDBACK;

            startupInfoStruct.cb = (uint)Marshal.SizeOf(startupInfoStruct);

            // Win32.NativeConstants.CREATE_BREAKAWAY_FROM_JOB может иметь проблемы
            // https://blog.csdn.net/jpexe/article/details/49661479

            if (!Win32.NativeMethods.CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false, Win32.NativeConstants.CREATE_SUSPENDED, IntPtr.Zero, WorkingDirectory, ref startupInfoStruct, out processInformationStruct))
            {
                throw new Exception("Ошибка WinAPI CreateProcessW. Код ошибки " + Win32.NativeMethods.GetLastError());
            }


            if (!Win32.NativeMethods.AssignProcessToJobObject(jobHandle, processInformationStruct.hProcess))
            {
                throw new Exception("Ошибка WinAPI AssignProcessToJobObject. Код ошибки " + Win32.NativeMethods.GetLastError());
            }

            Win32.NativeMethods.ResumeThread(processInformationStruct.hThread);

            // Закрыть ненужные дескрипторы
            Win32.NativeMethods.CloseHandle(processInformationStruct.hThread);
            Win32.NativeMethods.CloseHandle(processInformationStruct.hProcess);

            // DWORD->unsigned int
            uint completionCode;

            // ULONG_PTR->unsigned int
            IntPtr completionKey;

            ///LPOVERLAPPED->_OVERLAPPED*
            IntPtr overlapped;

            while (
                Win32.NativeMethods.GetQueuedCompletionStatus(
                    ioPortHandle,
                    out completionCode,
                    out completionKey,
                    out overlapped,
                    Win32.NativeConstants.INFINITE
                    ) && !(
                    completionKey == jobHandle &&
                    completionCode == Win32.NativeConstants.JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO
                    )
                    )
            {
                // ничего не делать
            }

            // все готово
            Win32.NativeMethods.CloseHandle(jobHandle);
            Win32.NativeMethods.CloseHandle(ioPortHandle);

            Trace.WriteLine("[Примечание] Игра завершилась.");

        }






        private void SetCompatibility(string Fullname, string Compatibility)
        {
            if (!String.IsNullOrWhiteSpace(Compatibility))
                using (var registryKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
                {
                    registryKey.SetValue(Fullname, Compatibility);
                }
        }


        private void RunStep1_Prepare()
        {
            if (!Directory.Exists(this.ProfileDirectory))
            {
                Directory.CreateDirectory(this.ProfileDirectory);
            }
            Trace.WriteLine("[Примечание] Очистка рабочего каталога...");
            // Рабочий каталог должен быть пустым. В противном случае удалить все.
            if (Directory.Exists(this.RunningDirectory))
            {
                IO.ClearDirectory(this.RunningDirectory);
            }
            else
            {
                Directory.CreateDirectory(this.RunningDirectory);
            }

            Trace.WriteLine("[Примечание] Создание жестких ссылок для профилей...");
            // Перемещение профилей
            IO.CreateHardLinksOfFiles(this.ProfileDirectory, this.RunningDirectory, false, new List<string>() { ".INI" });

            Trace.WriteLine("[Примечание] Создание жестких ссылок для игровых файлов...");
            // Создание жестких ссылок рекурсивно - от листового узла к корню
            this.PrepareNode(this.Node);


            this.SetCompatibility(
                Path.Combine(this.RunningDirectory, this.Node.MainExecutable),
                this.Node.Compatibility);

            //TODO: Установить реестр, чтобы избежать брандмауэра

        }

        private void RunStep3_Clean()
        {
            Trace.WriteLine("[Примечание] Удаление игровых файлов...");
            // Очистка узла
            this.CleanNode(this.Node);

            Trace.WriteLine("[Примечание] Удаление пустых папок...");
            // Удаление ненужных папок
            if (IO.RemoveEmptyFolders(this.RunningDirectory))
            {
                Directory.CreateDirectory(this.RunningDirectory);
            }
            else
            {
                Trace.WriteLine("[Примечание] Сохранение профилей...");
                // Сохранение профилей
                IO.CreateHardLinksOfFiles(this.RunningDirectory, this.ProfileDirectory, true, new List<string>() { ".INI" });
            }

            IO.ClearDirectory(this.RunningDirectory);

            /*
            // Удаление каталога
            System.IO.Directory.Delete(this.RunningDirectory, true);
            */

        }

        /// <summary>
        /// Выполняет все действия для запуска игры с указанными аргументами. 
        /// Запускается и ожидает подтверждения пользователя о завершении игры. Не блокирует поток.
        /// Это обходной путь для Windows 7 и более ранних версий.
        /// Этот метод ДОЛЖЕН быть запущен в потоке UI.
        /// </summary> 
        /// <param name="parent">Родительское окно MessageBox.</param>
        public void RunLegacyAsync(System.Windows.Window parent)
        {
            System.ComponentModel.BackgroundWorker worker1 = new System.ComponentModel.BackgroundWorker();

            worker1.DoWork += (object worker1_sender, System.ComponentModel.DoWorkEventArgs worker1_e) =>
            {
                RunStep1_Prepare();

                new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = Path.Combine(this.RunningDirectory, this.Node.MainExecutable),
                        Arguments = this.Node.Arguments,
                        WorkingDirectory = this.RunningDirectory
                    }
                }.Start();

                // Ждем 10 секунд
                System.Threading.Thread.Sleep(10000);
            };
            worker1.RunWorkerCompleted += (object worker1_sender, System.ComponentModel.RunWorkerCompletedEventArgs worker1_e) =>
            {
                if (worker1_e.Error != null)
                {
                    this.RunWorkerCompleted(worker1_sender, worker1_e);
                }
                else
                {
                    // продолжить
                    System.Windows.MessageBox.Show(parent, "Вы по-прежнему используете Windows 7 или более раннюю версию.\n Это слишком старая версия, поэтому мы не можем определить, завершилась ли игра. \n Нажмите кнопку OK, когда игра завершится.",
                   "Рекомендуется обновиться до Windows 10", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Exclamation);
                    System.Windows.MessageBox.Show(parent, "Нажимайте кнопку OK только после завершения игры.", "Двойная проверка",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Exclamation);

                    // уведомление для не особо внимательных
                    for (var i = 0; i < 5; ++i)
                    {
                        Process[] ps = Process.GetProcesses();
                        if (ps.Count() == 0) break;

                        foreach (Process p in ps)
                        {
                            string exePath;
                            try {
                                exePath = Path.GetDirectoryName(p.MainModule.FileName);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            
                            System.Diagnostics.Debug.WriteLine(exePath);
                            System.Diagnostics.Debug.WriteLine(this.RunningDirectory);

                            if (this.RunningDirectory.Contains(exePath))
                            {
                                System.Windows.MessageBox.Show(parent, "Нажимайте кнопку OK только после завершения игры.", "Двойная проверка",
                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Exclamation);
                                break;
                            }
                        }
                    }

                    Trace.WriteLine("[Примечание] Игра завершилась.");

                    System.ComponentModel.BackgroundWorker worker2 = new System.ComponentModel.BackgroundWorker();

                    worker2.DoWork += (object worker2_sender, System.ComponentModel.DoWorkEventArgs worker2_e) =>
                    {
                        // ждать 3 секунды. на всякий случай
                        System.Threading.Thread.Sleep(3000);
                        RunStep3_Clean();
                    };

                    worker2.RunWorkerCompleted += (object worker2_sender, System.ComponentModel.RunWorkerCompletedEventArgs worker2_e) =>
                    {
                        this.RunWorkerCompleted(worker2_sender, worker2_e);
                    };

                    worker2.RunWorkerAsync();
                }

            };

            worker1.RunWorkerAsync();

        }

        /// <summary>
        /// Выполняет все действия для запуска игры с указанными аргументами. 
        /// Ждет завершения игры. Не блокирует поток.
        /// Поддерживается только для Windows 8 или новее. 
        /// </summary>
        public void RunAsync()
        {
            System.ComponentModel.BackgroundWorker worker1 = new System.ComponentModel.BackgroundWorker();

            worker1.DoWork += (object worker1_sender, System.ComponentModel.DoWorkEventArgs worker1_e) =>
            {
                Run();
            };

            worker1.RunWorkerCompleted += (object worker1_sender, System.ComponentModel.RunWorkerCompletedEventArgs worker1_e) =>
            {
                this.RunWorkerCompleted(worker1_sender, worker1_e);
            };

            worker1.RunWorkerAsync();

        }

        /// <summary>
        /// Происходит, когда фоновая операция завершена, отменена или вызвала исключение.
        /// </summary>
        public event System.ComponentModel.RunWorkerCompletedEventHandler RunWorkerCompleted;

        /// <summary>
        /// Выполняет все действия для запуска игры с указанными аргументами. 
        /// Ждет завершения игры. Блокирует поток.
        /// Поддерживается только для Windows 8 или новее. 
        /// </summary>
        public void Run()
        {
            RunStep1_Prepare();
            RunStep2_RunAndWait();

            // ждать 3 секунды. на всякий случай
            System.Threading.Thread.Sleep(3000);
            RunStep3_Clean();
        }


    }
}
