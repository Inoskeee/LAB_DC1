using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Remoting.Messaging;

namespace Pipes
{
    public partial class frmMain : Form
    {
        private Int32 PipeHandle;                                                       // дескриптор канала
        private Int32 ConnectPipe;

        private string PipeName = "\\\\" + Dns.GetHostName() + "\\pipe\\ServerPipe";    // имя канала, Dns.GetHostName() - метод, возвращающий имя машины, на которой запущено приложение
        private Thread t;                                                               // поток для обслуживания канала
        private bool _continue = true;                                                  // флаг, указывающий продолжается ли работа с каналом
        
        private List<string> Pipes = new List<string>();

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            // создание именованного канала
            PipeHandle = DIS.Import.CreateNamedPipe($"\\\\.\\pipe\\ServerPipe", DIS.Types.PIPE_ACCESS_DUPLEX, DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT, DIS.Types.PIPE_UNLIMITED_INSTANCES, 0, 1024, DIS.Types.NMPWAIT_WAIT_FOREVER, (uint)0);

            // вывод имени канала в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + PipeName;


            // создание потока, отвечающего за работу с каналом
            t = new Thread(ReceiveMessage);
            t.Start();
        }

        private void ReceiveMessage()
        {
            string messageToClient = null;
            string msg = "";            // прочитанное сообщение
            uint realBytesReaded = 0;   // количество реально прочитанных из канала байтов

            // входим в бесконечный цикл работы с каналом
            while (_continue)
            {
                if (DIS.Import.ConnectNamedPipe(PipeHandle, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(PipeHandle);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(PipeHandle, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    msg = Encoding.Unicode.GetString(buff);                                 // выполняем преобразование байтов в последовательность символов

                    if (!string.IsNullOrEmpty(msg))
                    {
                        string[] information = msg.Split('^');
                        string pipeName = information[0];
                        string message = information[1].Replace("\0","");
                        if (message == "system_test")
                        {
                            TestSystemSend(pipeName);
                        }
                        else if(message == "loadMessages")
                        {
                            Pipes.Add(pipeName);
                            rtbMessages.Invoke((MethodInvoker)delegate
                            {
                                if (string.IsNullOrEmpty(rtbMessages.Text))
                                {
                                    rtbMessages.Text += $"Пользователь {pipeName} присоединился!";
                                }
                                else
                                {
                                    rtbMessages.Text += $"\nПользователь {pipeName} присоединился!";
                                }
                            });
                            ConnectChat(pipeName);
                        }
                        else if(message == "userExit")
                        {
                            Pipes.Remove(pipeName);
                            rtbMessages.Invoke((MethodInvoker)delegate
                            {
                                if (string.IsNullOrEmpty(rtbMessages.Text))
                                {
                                    rtbMessages.Text += $"Пользователь {pipeName} вышел!";
                                }
                                else
                                {
                                    rtbMessages.Text += $"\nПользователь {pipeName} вышел!";
                                }
                            });
                            DisconnectChat(pipeName);
                        }
                        else
                        {
                            rtbMessages.Invoke((MethodInvoker)delegate
                            {
                                if (message != "")
                                {
                                    if (string.IsNullOrEmpty(rtbMessages.Text))
                                    {
                                        rtbMessages.Text += "" + message;
                                    }
                                    else
                                    {
                                        rtbMessages.Text += "\n" + message;
                                    }
                                }
                            });
                            if (Pipes.Contains(pipeName))
                            {
                                messageToClient = message;
                            }
                            else
                            {
                                Pipes.Add(pipeName);
                            }
                            MessageSend(messageToClient);
                            messageToClient = null;
                        } 
                    }

                    DIS.Import.DisconnectNamedPipe(PipeHandle);
                    Thread.Sleep(500);                                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента

                }
            }
        }

        public void DisconnectChat(string pipeName)
        {
            string msg = "";

            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
            foreach (var pipe in Pipes)
            {
                if (pipe != pipeName)
                {
                    msg = $"Пользователь {pipeName} вышел!";
                    BytesWritten = 0;  // количество реально записанных в канал байт
                    buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

                    ConnectPipe = DIS.Import.CreateFile($"\\\\.\\pipe\\{pipe}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

                    DIS.Import.WriteFile(ConnectPipe, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

                    DIS.Import.CloseHandle(ConnectPipe);
                }
            }
        }

        public void ConnectChat(string pipeName)
        {
            string msg = "";
            rtbMessages.Invoke((MethodInvoker)delegate
            {
                msg = rtbMessages.Text;
            });

            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            ConnectPipe = DIS.Import.CreateFile($"\\\\.\\pipe\\{Pipes.Last()}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

            DIS.Import.WriteFile(ConnectPipe, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(ConnectPipe);

            foreach(var pipe in Pipes)
            {
                if(pipe != pipeName)
                {
                    msg = $"Пользователь {pipeName} присоединился!";
                    BytesWritten = 0;  // количество реально записанных в канал байт
                    buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

                    ConnectPipe = DIS.Import.CreateFile($"\\\\.\\pipe\\{pipe}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

                    DIS.Import.WriteFile(ConnectPipe, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

                    DIS.Import.CloseHandle(ConnectPipe);
                }
            }
        }

        public void TestSystemSend(string pipeName)
        {
            string msg = "";
            foreach(var pipe in Pipes)
            {
                msg += pipe+"/";
            }

            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            ConnectPipe = DIS.Import.CreateFile($"\\\\.\\pipe\\{pipeName}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

            DIS.Import.WriteFile(ConnectPipe, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(ConnectPipe);
        }

        public void MessageSend(string message)
        {
            string msg = message;
            if (string.IsNullOrEmpty(msg))
            {
                rtbMessages.Invoke((MethodInvoker)delegate
                {
                    msg = rtbMessages.Text;
                });

                uint BytesWritten = 0;  // количество реально записанных в канал байт
                byte[] buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

                ConnectPipe = DIS.Import.CreateFile($"\\\\.\\pipe\\{Pipes.Last()}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

                DIS.Import.WriteFile(ConnectPipe, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

                DIS.Import.CloseHandle(ConnectPipe);                                                                 // закрываем дескриптор канала
                
            }
            else
            {
                uint BytesWritten = 0;  // количество реально записанных в канал байт
                byte[] buff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

                foreach (var pipe in Pipes)
                {
                    ConnectPipe = DIS.Import.CreateFile($"\\\\.\\pipe\\{pipe}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

                    DIS.Import.WriteFile(ConnectPipe, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

                    DIS.Import.CloseHandle(ConnectPipe);                                                                 // закрываем дескриптор канала
                }
            }
          
        }


        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);

        }
    }
}