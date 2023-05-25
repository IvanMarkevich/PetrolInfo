using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PetrolInfo.Data
{
    public class CardInfo
    {
        public int Nomer { get; set; }
        public string Filial { get; set; }
        public string Client { get; set; }
        public string Owner { get; set; }
        public string State { get; set; }
        public DateTime DateState { get; set; }
        public string Reason { get; set; }
        public DateTime DateStop { get; set; }
        public List<OfflineCell> OfflineCells { get; set; }
        public string Type { get; set; }
        public decimal OnlineBalance { get; set; }
        public List<OnlineCell> OnlineCells { get; set; }
    }

    public class OfflineCell
    {
        public string Service { get; set; }
        public string Sheme { get; set; }
        public decimal Balance { get; set; }
        public decimal Limit { get; set; }
        public string TermOfLimit { get; set; }
        public string TypeOfLimit { get; set; }
    }

    public class OnlineCell
    {
        public string Service { get; set; }
        public string DLimit { get; set; }
        public string WLimit { get; set; }
        public string MLimit { get; set; }
    }

    public class CardHistoryEntry
    {
        public DateTime When { get; set; }
        public string Owner { get; set; }
        public string State { get; set; } 
        public string WhoServ { get; set; }
        public string Comment { get; set; }
    }

    public class OPEntry
    {
        public DateTime CreateDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Service { get; set; }
        public decimal Summa { get; set; }
        public string State { get; set; }
        public DateTime? RcvDate { get; set; }
    }

    public class OILEntry
    {
        public DateTime CreateDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Service { get; set; }
        public decimal Summa { get; set; }
        public string State { get; set; }
        public DateTime? RcvDate { get; set; }
    }

    public class Tranz
    {
        public DateTime DateOf { get; set; }
        public DateTime DateIn { get; set; }
        public string Service { get; set; }
        public decimal Amount { get; set; }
        public int PosEmitent { get; set; }
        public int TONum { get; set; }
        public string TOName { get; set; }
        public string TOAddres { get; set; }
        public string TypeOfServ { get; set; }
    }
}
