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
using System.IO;
using System.Threading;
using System.Windows.Forms.VisualStyles;

namespace Pipes
{
    public partial class ClientForm : Form
    {
        private Int32 PipeHandle;   // дескриптор канала
        private Int32 PipeConnect;   // дескриптор канала
        private string pipeName;
        private string connectServer;

        Thread t;
        bool _continue = true;
        // конструктор формы
        public ClientForm(string pipe, string connectr)
        {
            InitializeComponent();

            Random rnd = new Random();
            pipeName = pipe;
            connectServer = connectr;
            PipeHandle = DIS.Import.CreateNamedPipe($"\\\\.\\pipe\\{pipeName}", DIS.Types.PIPE_ACCESS_DUPLEX, DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT, DIS.Types.PIPE_UNLIMITED_INSTANCES, 0, 1024, DIS.Types.NMPWAIT_WAIT_FOREVER, (uint)0);

            this.Text += $" {Dns.GetHostName()}/{pipeName}";   // выводим имя текущей машины в заголовок формы
            t = new Thread(ReceiveMessage);
            t.Start();


            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(pipeName + "^loadMessages");    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            // открываем именованный канал, имя которого указано в поле tbPipe
            PipeConnect = DIS.Import.CreateFile(connectServer, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

            DIS.Import.WriteFile(PipeConnect, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(PipeConnect);                                                                 // закрываем дескриптор канала
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(pipeName + "^" + pipeName + " >> " + tbMessage.Text);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
            
            // открываем именованный канал, имя которого указано в поле tbPipe
            PipeConnect = DIS.Import.CreateFile(connectServer, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            
            DIS.Import.WriteFile(PipeConnect, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(PipeConnect);                                                                 // закрываем дескриптор канала
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
                    msg = Encoding.Unicode.GetString(buff);                                 // выполняем преобразование байтов в последовательность символов
                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        if (msg != "")
                        {
                            if (string.IsNullOrEmpty(rtbMessages.Text))
                            {
                                rtbMessages.Text += "" + msg;
                            }
                            else
                            {
                                rtbMessages.Text += "\n" + msg;
                            }
                        }       
                    });

                    DIS.Import.DisconnectNamedPipe(PipeHandle);                             // отключаемся от канала клиента 
                    Thread.Sleep(500);                                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                }
            }
        }

        private void ClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт
            byte[] buff = Encoding.Unicode.GetBytes(pipeName + "^userExit");    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            // открываем именованный канал, имя которого указано в поле tbPipe
            PipeConnect = DIS.Import.CreateFile(connectServer, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);

            DIS.Import.WriteFile(PipeConnect, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(PipeConnect);                                                                 // закрываем дескриптор канала
            
            Environment.Exit(0);
        }
    }
}
