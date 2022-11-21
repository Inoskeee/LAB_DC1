using Pipes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PipesClient
{
    public partial class MainForm : Form
    {
        private Int32 PipeHandle;
        private Int32 PipeConnect;
        int testName;
        Thread t;
        bool _continue = true;
        string[] clients = null;
        public MainForm()
        {
            InitializeComponent();
            Random rnd = new Random();
            testName = rnd.Next(10000, 99999);
            PipeHandle = DIS.Import.CreateNamedPipe($"\\\\.\\pipe\\{testName}", DIS.Types.PIPE_ACCESS_DUPLEX, DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT, DIS.Types.PIPE_UNLIMITED_INSTANCES, 0, 1024, DIS.Types.NMPWAIT_WAIT_FOREVER, (uint)0);

            t = new Thread(ReceiveMessage);
            t.Start();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(login.Text))
            {
                if (!string.IsNullOrEmpty(tbPipe.Text))
                {
                    uint BytesWritten = 0;  // количество реально записанных в канал байт
                    byte[] buff = Encoding.Unicode.GetBytes(testName + "^system_test");    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

                    // открываем именованный канал, имя которого указано в поле tbPipe
                    PipeConnect = DIS.Import.CreateFile(tbPipe.Text, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
                    DIS.Import.WriteFile(PipeConnect, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал
                    DIS.Import.CloseHandle(PipeConnect);                                                                 // закрываем дескриптор канала
                    Thread.Sleep(1000);
                    if(clients != null)
                    {
                        if (!clients.Contains(login.Text))
                        {
                            ClientForm SF = new ClientForm(login.Text, tbPipe.Text);
                            SF.Show();
                            this.Enabled = false;
                        }
                        else
                        {
                            MessageBox.Show("Клиент с таким логином уже в чате!");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Нет подключения к чату!");
                    }
                }
                else
                {
                    MessageBox.Show("Адрес сервера не может быть пустым!");
                }
            }
            else
            {
                MessageBox.Show("Логин не может быть пустым!");
            }

            
        }

        private void ReceiveMessage()
        {
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
                    msg = Encoding.Unicode.GetString(buff).Replace("\0","");                                 // выполняем преобразование байтов в последовательность символов
                    if(msg != "")
                    {
                        clients = msg.Split('/');
                    }
                    else
                    {
                        clients = new string[] { "" };
                    }
                    DIS.Import.DisconnectNamedPipe(PipeHandle);                             // отключаемся от канала клиента 
                    Thread.Sleep(500);                                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
