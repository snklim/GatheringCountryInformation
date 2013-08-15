using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Data;
using System.Xml;
using System.ServiceModel.Channels;
using System.IO;
using System.ServiceModel.Web;

namespace GatheringCountryInformation
{
    class World
    {
        public List<Continent> Continents { get; private set; }

        public World(CountryInformationServiceSoapClient service, TaskCompletionSource<bool> pTcs) 
        {
            Continents = new List<Continent>();

            Console.WriteLine("Retrieving the list of continents");
            
            Task t1 = service.GetContinentsAsync().ContinueWith(t =>
            {
                List<Task<bool>> tcss = new List<Task<bool>>();

                DataSet data = t.Result;
                foreach (DataRow row in data.Tables[0].Rows)
                {
                    TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                    Continents.Add(new Continent(service, tcs, row[0].ToString(), row[1].ToString()));
                    tcss.Add(tcs.Task);
                }

                Task.WhenAll(tcss.ToArray()).ContinueWith(ts =>
                {
                    pTcs.SetResult(true);
                });
            });            
        }
    }

    class Continent
    {
        public string Code { get; private set; }
        public string Name { get; private set; }
        public double ExecutionTime { get; private set; }

        public List<Country> Countries { get; private set; }

        public Continent(CountryInformationServiceSoapClient pService, TaskCompletionSource<bool> pTcs, string pCode, string pName)
        {
            Countries = new List<Country>();
            Code = pCode;
            Name = pName;

            Console.WriteLine("Retrieving the list of countries for continent {0}", Name);

            Task t1 = pService.GetCountriesByContinentAsync(Code).ContinueWith(t =>
            {
                List<Task<bool>> tcss = new List<Task<bool>>();

                DateTime d1 = DateTime.Now;

                DataSet data = t.Result;
                foreach (DataRow row in data.Tables[0].Rows)
                {
                    TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                    Countries.Add(new Country(pService, tcs, row[0].ToString(), row[1].ToString(), row[2].ToString()));
                    tcss.Add(tcs.Task);
                }

                Task.WhenAll(tcss.ToArray()).ContinueWith(ts =>
                {
                    DateTime d2 = DateTime.Now;
                    ExecutionTime = (d2 - d1).TotalSeconds;
                    pTcs.SetResult(true);
                });
            });
        }
    }

    class Country
    {
        public string Iso2 { get; private set; }
        public string Iso3 { get; private set; }
        public string Name { get; private set; }

        public string Capital { get; private set; }
        public long Population { get; private set; }

        public Country(CountryInformationServiceSoapClient pService, TaskCompletionSource<bool> pTcs, string pIso2, string pIso3, string pName)
        {
            Iso2 = pIso2;
            Iso3 = pIso3;
            Name = pName;

            Console.WriteLine("Retrieving capital and population for country {0}", Name);

            Task t1 = pService.GetCapitalByCountryAsync(Name).ContinueWith(t =>
            {
                Capital = t.Result;

                Console.WriteLine("Capital retrieved for country {0}", Name);
            });
            Task t2 = pService.GetPopulationByCountryAsync(Name).ContinueWith(t =>
            {
                Population = long.Parse(t.Result);

                Console.WriteLine("Population retrieved for country {0}", Name);
            });

            Task.WhenAll(t1, t2).ContinueWith(ts =>
            {
                pTcs.SetResult(true);
            });
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            System.Net.ServicePointManager.Expect100Continue = false;

            var service = new CountryInformationServiceSoapClient("CountryInformationServiceSoap12");

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            World world = new World(service, tcs);

            tcs.Task.Wait();

            Console.WriteLine("Press any key to continue");

            Console.ReadLine();

            using (Stream s = File.Create(@"ContinentName.xml"))
            using (XmlWriter w = XmlWriter.Create(s))
            {
                w.WriteStartElement("Continents");
                foreach (Continent continent in world.Continents)
                {
                    w.WriteStartElement("Continent");
                    w.WriteAttributeString("Name", continent.Name);
                    w.WriteAttributeString("Code", continent.Code);
                    
                    foreach (Country country in continent.Countries)
                    {
                        w.WriteStartElement("Country");

                        w.WriteAttributeString("ISOCode", country.Iso2);
                        w.WriteAttributeString("Name", country.Name);

                        w.WriteElementString("Capital", country.Capital);
                        w.WriteElementString("Pupolation", country.Population.ToString());

                        w.WriteEndElement();
                    }

                    w.WriteStartElement("Execution");
                    w.WriteAttributeString("Time", continent.ExecutionTime.ToString());
                    w.WriteEndElement();

                    w.WriteEndElement();
                }

                w.WriteEndElement();
            }

            Console.WriteLine("File created. Press any key to continue.");
            Console.ReadLine();
        }
    }
}
