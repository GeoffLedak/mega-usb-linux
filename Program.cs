using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace mega_usb
{
    class Program
    {



        static void Main(string[] args)
        {

            SerialPort port;

            string option = null;


            try
            {



                System.Console.WriteLine("");
                System.Console.WriteLine("Mega EverDrive usb tool v2.0");
                if (args.Length == 0)
                {
                    return;
                }
                System.Console.WriteLine("search...");
                port = searchCart();
                System.Console.WriteLine("MegaED detected on " + port.PortName);


                FileStream f = File.OpenRead(args[0]);
                int len = (int)f.Length;
                if (len % 65536 != 0) len = len / 65536 * 65536 + 65536;
                byte[] buff = new byte[len];
                f.Read(buff, 0, (int)f.Length);
                f.Close();

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Contains("MEGA.BIN") || args[i].Contains(".RBF") || args[i].Contains(".rbf"))
                    {
                        v2_sysTools(args, port);
                        port.Close();
                        return;
                    }
                }

                if (args.Length == 1)
                {
                    option = args[0].Contains("MEGAOS.bin") ? "-o" : args[0].Contains(".sms") | args[0].Contains(".SMS") ? "-sms" : "-smd";
                    if (buff[261] == 'S' && buff[262] == 'S' && buff[263] == 'F' && buff[264] == ' ') option = "-ssf";

                }
                else
                {
                    option = args[1];
                }



                if (option.Equals("-o") || option.Equals("-fo"))
                {

                    loadOS(option, buff, port);
                }
                else
                {

                    loadGame(option, buff, port);
                }



            }
            catch (Exception x)
            {
                System.Console.WriteLine("\nERROR: " + x.Message);
            }




        }




        static void loadOS(string arg, byte[] data, SerialPort port)
        {

            if (data.Length > 0x100000) throw new Exception("OS file is too big");
            port.ReadTimeout = 1000;
            port.WriteTimeout = 1000;
            byte[] tx = new byte[1];


            if (arg.Equals("-fo"))
            {
                System.Console.WriteLine("firmware loading...");
                tx[0] = (byte)(64+32);
                port.Write("*f");
                port.Write(tx, 0, 1);
                if (port.ReadByte() != 'k') throw new Exception("unexpected response");
                sendData(data, port, 0x8400, 0x18000);

                port.ReadTimeout = 200;
                port.WriteTimeout = 200;
                Thread.Sleep(1000);

                for (int i = 0; ; i++)
                {
                    if (i > 10) throw new Exception("os reloading timeout");

                    try
                    {
                        for (; ; ) port.ReadByte();
                    }
                    catch (Exception) { }

                    try
                    {
                        port.Write("*T");
                        if (port.ReadByte() != 'k') throw new Exception("unexpected response");
                        break;
                    }
                    catch (Exception) { }


                }



                port.ReadTimeout = 1000;
                port.WriteTimeout = 1000;
            }

            System.Console.WriteLine("OS loading...");
            tx[0] = (byte)(data.Length/512/128);
            port.Write("*o");
            port.Write(tx, 0, 1);
            if (port.ReadByte() != 'k') throw new Exception("unexpected response");
            sendData(data, port, 0, data.Length);

            System.Console.WriteLine("done!");
        }





        static void loadGame(string arg, byte []data, SerialPort port)
        {

            byte[] tx = new byte[1];
            // System.Console.WriteLine("game loadin request...");
            port.ReadTimeout = 1000;
            port.WriteTimeout = 1000;
            port.Write("*g");
            tx[0] = (byte)(data.Length / 512 / 128);

            port.Write(tx, 0, 1);
            //System.Console.WriteLine("len: " + data.Length);

            if (port.ReadByte() != 'k') throw new Exception("unexpected response");
            if (data.Length > 0xf00000) throw new Exception("file size is too big");



            sendData(data, port, 0, (int)data.Length);

            //System.Console.WriteLine("start game...");
            if (arg.Equals("-sms"))
            {
                port.Write("*rs");
            }
            else if (arg.Equals("-os"))
            {
                port.Write("*ro");
            }
            else if (arg.Equals("-cd"))
            {
                port.Write("*rc");
            }
            else if (arg.Equals("-m10"))
            {
                port.Write("*rM");
            }
            else if (arg.Equals("-ssf"))
            {
                port.Write("*rS");
            }

            else
            {
                port.Write("*rm");
            }

            if (port.ReadByte() != 'k') throw new Exception("unexpected response");

            System.Console.WriteLine("done!");
        }

        static void sendData(byte []data, SerialPort port, int offset, int len)
        {
            int block_len = 0x8000;// len % 0x10000 == 0 ? 0x10000 : 0x8000;

            System.Console.Write("load data");
            DateTime dt = DateTime.Now;
            for (int i = 0; i < len; i += block_len)
            {
                port.Write(data, i + offset, block_len);
                if (i % 65536 == 0) Console.Write(".");
            }
            System.Console.WriteLine("");
            System.Console.WriteLine("time: " + (System.DateTime.Now - dt).Ticks / 10000 + "ms");
            if (port.ReadByte() != 'd') throw new Exception("unexpected response");
        }

        static SerialPort searchCart()
        {
            string[] port_list = SerialPort.GetPortNames();
            SerialPort port = null;

            for (int i = 0; i < port_list.Length; i++)
            {
                try
                {
                    port = new SerialPort(port_list[i]);
                    port.ReadTimeout = 200;
                    port.WriteTimeout = 200;
                    port.Open();
                    port.ReadExisting();
                    port.Write("    *T");



                    if (port.ReadByte() == (byte)'k') return port;
                    port.Close();
                }
                catch (Exception)
                {
                    if (port.IsOpen) port.Close();
                }

            }

            throw new Exception("MegaED is not detected");
        }


        static void v2_sysTools(string[] args, SerialPort port)
        {
            port.ReadTimeout = 3000;
            port.WriteTimeout = 3000;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains(".RBF") || args[i].Contains(".rbf"))
                {
                    v2_loadFpga(args[i], port);
                    //Thread.Sleep(500);
                    port.Write("*T");
                    if (port.ReadByte() != 'k') throw new Exception("unexpected response");

                }
            }



            for (int i = 0; i < args.Length; i++)
            {

                if (args[i].Contains("MEGA.BIN"))
                {
                    v2_loadOS(args[i], port);
                    //Thread.Sleep(500);
                    port.Write("*T");
                    if (port.ReadByte() != 'k') throw new Exception("unexpected response");
                }
            }
        }

        static void v2_loadOS(string filename, SerialPort port)
        {

            System.Console.Write("OS loading");
            FileStream f = File.OpenRead(filename);
            byte[] buff = new byte[f.Length];
            f.Read(buff, 0, buff.Length);
            f.Close();

            port.Write("*o");
            port.Write(new byte[] { (byte)(buff.Length / 512 >> 8), (byte)(buff.Length / 512) }, 0, 2);

            int block_len = 2048;
            for (int i = 0; i < buff.Length; i += block_len)
            {
                port.Write(buff, i, block_len);
                if (i % 8192 == 0) Console.Write(".");
            }

            port.Write("*R");
            Console.WriteLine("ok");


        }

        static void v2_loadFpga(string filename, SerialPort port)
        {
            System.Console.Write("Firmware loading");
            FileStream f = File.OpenRead(filename);
            byte[] buff = new byte[0x18000];
            for (int i = 0; i < buff.Length; i++) buff[i] = 0xff;
            f.Read(buff, 0, buff.Length);
            f.Close();

            port.Write("*f");
            port.Write(new byte[] { (byte)(buff.Length / 2 >> 8), (byte)(buff.Length / 2) }, 0, 2);

            int block_len = 2048;
            for (int i = 0; i < buff.Length; i += block_len)
            {
                port.Write(buff, i, block_len);
                if (i % 8192 == 0) Console.Write(".");
            }

            Console.WriteLine("ok");

        }


    }


}
