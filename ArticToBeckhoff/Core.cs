using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using TwinCAT.Ads;

namespace ArticToBeckhoff
{
    internal class Core
    {
        public struct address
        {
            public string name;
            public int addr;
            public string datatype;
            public bool writeable;
            public string twincat;
        }

        private DateTime heartbeat = DateTime.Now;
        private bool stop = false;
        private TcAdsClient ads;
        private TcAdsSymbolInfoLoader tcSymbolL;

        private Dictionary<string, string> lastValues = new Dictionary<string, string>();

        public async Task Stop()
        {
            stop = true;

            ads.Dispose();
            ads = null;
            Thread.Sleep(500);

        }

        public async Task Start()
        {
            Logger("Starting Health Checks");
            var thread = new Thread(() =>
            {
                Heartbeat_Check();

            });

            Logger("Loading addresses.csv");
            List<address> modbusitems = loadAddresses();

            Logger("Connecting to Modbus " + Properties.Settings.Default.IP + " " + Properties.Settings.Default.Port);
            EasyModbus.ModbusClient modbusClient = new EasyModbus.ModbusClient();
            //modbusClient.Baudrate = 2400;
            //modbusClient.SerialPort = Properties.Settings.Default.Port;

            modbusClient.Connect(Properties.Settings.Default.IP,Properties.Settings.Default.Port);

        
            
            Logger("Connecting to ADS");

            ads = new TcAdsClient();
            ads.Connect(AmsNetId.Local, 801);

            tcSymbolL = ads.CreateSymbolInfoLoader();

            while (true)
            {
                heartbeat = DateTime.Now;

                // Reads Modbus data and pushes to TwinCAT
                foreach (address item in modbusitems)
                {
                    if (item.twincat != null && item.twincat.Length > 3)
                    {
                        int[] serverResponse = null;

                        Logger("Reading: " + item.name);
                        if (modbusClient.Connected)
                        {
                            serverResponse = modbusClient.ReadHoldingRegisters(item.addr, 1);
                        } else
                        {
                            Logger("Serial Port not connected, not reading...");
                        }
                        
                        try
                        {
                            if (item.datatype == "Bool")
                            {
                                if (serverResponse[0] == 0)
                                {
                                    ads.WriteSymbol(item.twincat, false, true);
                                }
                                else
                                {
                                    ads.WriteSymbol(item.twincat, true, true);
                                }
                            }

                            if (item.datatype == "Int16")
                            {
                                ads.WriteSymbol(item.twincat, serverResponse[0], true);
                            }
                        }
                        catch (Exception e)
                        {

                            Console.WriteLine(e.Message);
                        }
                    }
                }

                // Check for WRITE tags in Twincat, and pushes to Modbus
                foreach (address item in modbusitems)
                {
                    if (item.writeable == true && item.twincat != null && item.twincat.Length > 3)
                    {
                        ITcAdsSymbol symbol_tag = tcSymbolL.FindSymbol(item.twincat + "_WRITE");

                        if (symbol_tag != null)
                        {
                            object result = ads.ReadSymbol(symbol_tag);

                            if (LastValueSameCheck(ref lastValues, item.name, result.ToString()) == false)
                            {
                                Logger("Modbus Write:" + item.name + " Value: " + result.ToString());

                                if (modbusClient.Connected)
                                {
                                    if (item.datatype == "Bool")
                                    {

                                        if (Convert.ToBoolean(result) == true)
                                        {
                                            modbusClient.WriteSingleRegister(item.addr, 1);
                                        } else
                                        {
                                            modbusClient.WriteSingleRegister(item.addr, 0);
                                        }
                                    }
                                    else if (item.datatype == "Int16")
                                    {
                                        modbusClient.WriteSingleRegister(item.addr, Convert.ToInt32(result));
                                    }
                                } else
                                {
                                    Logger("Serial port closed, not writing...");
                                }
                            } 
                        }
                        else
                        {
                            Logger("TwinCAT WRITE tag not found: " + item.twincat + "_WRITE");
                        }

                    }
                }
                  
                Thread.Sleep(5000);
            } 
        } 

        private bool LastValueSameCheck(ref Dictionary<string, string> dict, string name, object value)
        {
            if (dict.ContainsKey(name))
            {
                if (dict[name] == value.ToString())
                {
                    return true;
                }

                dict.Remove(name);
                dict.Add(name, value.ToString());

                return false;
            }
            else
            {
                dict.Add(name, value.ToString());
                return false;
            }
        }

        private void Heartbeat_Check()
        {
            while (!stop)
            {
                Thread.Sleep(10000);

                System.TimeSpan diff_mins = DateTime.Now - heartbeat;

                Logger($"Health Check {diff_mins.TotalSeconds}");

                if (diff_mins.TotalSeconds >= 30)
                {
                    stop = true;
                    ads.Dispose();
                    ads = null;
                    Thread.Sleep(500);


                    Logger($"ADS ({ads.IsConnected}) or RouterState ({ads.RouterState}) are not correct.  Terminating App");
                    System.Environment.FailFast("MagnaOTEdge - ADS connection issue");

                    return;
                }
            }
        }

        static List<address> loadAddresses()

        {
            string buffer = System.IO.File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + @"\addresses.csv");
            string[] lines = buffer.Split('\n');
            int position = 0;

            List<address> addresses = new List<address>();

            foreach (var line in lines)
            {
                string[] fields = line.Replace('\r', ' ').Split(',');

                address item = new address();
                item.name = fields[1];

                try
                {
                    item.addr = Convert.ToInt32(fields[2]);
                }
                catch (Exception)
                {

                    item.addr = -1;
                }

                item.datatype = fields[3];


                if (fields[4] == "1")
                {
                    item.writeable = true;
                }
                else
                {
                    item.writeable = false;
                }

                if (fields.Length == 6)
                {
                    item.twincat = fields[5].Trim();
                }


                if (position > 0)
                {
                    addresses.Add(item);
                }


                position++;

            }

            return addresses;

        }


        private static void Logger(string Message, bool debug = false)
        {
            Console.WriteLine("[" + DateTime.Now + "] " + Message);

        }

    }
}
