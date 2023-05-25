using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace PetrolInfo.Data
{
    public class InfoService
    {
        private readonly IConfiguration Configuration;
        public InfoService(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        

        public async Task<List<OneRow>> GetInkassInfoAsync()
        {
            
            string constring = Configuration["constring"];
            List<OneRow> rows = new List<OneRow>();
            using (OracleConnection con = new OracleConnection(constring))
            {
                
                

                using (OracleCommand cmd = con.CreateCommand())
                {
                    con.Open();
                    
                    cmd.CommandText = $"select id_to, nazvanie, adres, to_date(summ,'yyyymmddhh24miss')"
                        + " from (select distinct id_to, max(summa) summ from ecfil052 where id_to > 0 group by id_to)"
                        + " join ecfil037 on(id_to = ID_TOCHKI_OBSLUGIVANIYA and ID_EMITENT= 276)"
                        + " where summ < to_char(trunc(sysdate),'yyyymmddhh24miss')"
                        + " and nazvanie not like('%ORLEN%') and nazvanie not like('%Тест%')" 
                        + " and nazvanie not like('%для ОП%')" 
                        + " and id_to not in (select id_to from ecfil030 where id_emitent = 276 and id_cond = 2) order by 4 ";
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        OneRow row = new OneRow
                        {
                            Nomer = reader.GetInt32(0),
                            Nazvanie = reader.GetString(1),
                            Adres = reader.GetString(2),
                            Date = reader.GetDateTime(3)
                        };
                        rows.Add(row);
                    }

                    reader.Dispose();
                }
                return await Task.FromResult(rows);
            }
        }

        public async Task<List<EmitentInfo>> GetEmitentInfoAsync()
        {
            string constring = Configuration["constring"];
            List<EmitentInfo> rows = new List<EmitentInfo>();
            using (OracleConnection con = new OracleConnection(constring))
            {



                using (OracleCommand cmd = con.CreateCommand())
                {
                    con.Open();

                    cmd.CommandText = $"select EMITENT_ID, emitent_balance from oc_onl_emitent_groups oeg"
                        + " join oc_onl_acquirer_emitent ooae on (oeg.EMITENT_GROUP_ID = ooae.EMITENT_GROUP_ID)"
                        + " join oc_onl_acquirer_emitent_params ep on (ooae.EMITENT_GROUP_ID = ep.EMITENT_GROUP_ID)"
                        + " where active = 1 and emitent_id <>276 order by 1";
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        EmitentInfo row = new EmitentInfo
                        {
                            EmitentNumber = reader.GetInt32(0),
                            Balance = reader.GetDecimal(1)
                        };
                        rows.Add(row);
                    }

                    reader.Dispose();
                }
                return await Task.FromResult(rows);
            }
        }

        public async Task<CardInfo> GetCardInfoAsync(int cardnumber)
        {
            CardInfo info = new CardInfo();
            info.Nomer = cardnumber;
            int filialNumber = 0;
            string constring = Configuration["constring_roc"];
            using (OracleConnection con = new OracleConnection(constring))
            {
                con.Open();
                try
                {
                    OracleTransaction transaction = con.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
                    
                    using (OracleCommand GetFilial = con.CreateCommand())
                    {
                        GetFilial.Transaction = transaction;
                        GetFilial.CommandText = $"select filial, filial_desc from pc_cards" +
                            $" join pc_filial_list on (filial = filial_number and" +
                            $" pc_filial_list.emitent = 276) where card_graf_num = {cardnumber}";
                        OracleDataReader reader = GetFilial.ExecuteReader();
                        while (reader.Read())
                        {
                            filialNumber = reader.GetInt32(0);
                            info.Filial = reader.GetString(1);
                        }
                        reader.Dispose();

                    }
                    

                    using (OracleCommand GetClient = con.CreateCommand())
                    {
                        GetClient.Transaction = transaction;
                        GetClient.CommandText = $"select name from ecfil002@m_{filialNumber}" +
                            $" where id_firmy = (select id_vladeltza from ecfil012@m_{filialNumber}" +
                            $" where gr_nomer = {cardnumber} and id_prinadlejnosti = 2)" +
                            $" union select fio from ecfil008@m_{filialNumber}" +
                            $" where id_cheloveca = (select id_vladeltza" +
                            $" from ecfil012@m_{filialNumber} where gr_nomer = {cardnumber} and" +
                            $" id_prinadlejnosti =3)";
                        try
                        {
                            info.Client = GetClient.ExecuteScalar().ToString();
                        }
                        catch
                        {
                            info.Client = "немає даних";
                        }
                    }

                    using (OracleCommand GetOwner = con.CreateCommand())
                    {
                        GetOwner.Transaction = transaction;
                        GetOwner.CommandText = $"select derjatel from ecfil012@m_{filialNumber}" +
                            $" where gr_nomer = {cardnumber}";
                        info.Owner = GetOwner.ExecuteScalar().ToString();
                    }

                    using (OracleCommand GetState = con.CreateCommand())
                    {
                        GetState.Transaction = transaction;
                        GetState.CommandText = $"select decode(ID_SOSTOYANYA,0,'не форматована'," +
                            $"1,'в роботі',2,'в ЧС',3,'згублена',4,'не видана',5,'заблокована'," +
                            $"7,'фірма в ЧС','невідомо') from ecfil012@m_{filialNumber}" +
                            $" where gr_nomer = {cardnumber}";
                        info.State = GetState.ExecuteScalar().ToString();
                    }

                    using (OracleCommand GetDates = con.CreateCommand())
                    {
                        GetDates.Transaction = transaction;
                        GetDates.CommandText = $"select k.DATA_VYDACHI, k.DATA_IZYATIYA," +
                            $" k.PRICHINA_NERABOCH_SOST from ecfil012@m_{filialNumber} k" +
                            $" where gr_nomer = {cardnumber}";
                        OracleDataReader reader = GetDates.ExecuteReader();
                        while (reader.Read())
                        {
                            DateTime datestart = reader.GetDateTime(0);
                            DateTime datestop = reader.GetDateTime(1);
                            if (info.State == "в роботі")
                                info.DateState = datestart;
                            else
                            {
                                info.DateState = datestop;
                                
                                info.Reason = reader.GetString(2);
                            }
                               

                        }
                        reader.Dispose();
                    }

                    using (OracleCommand GetDateBlock = con.CreateCommand())
                    {
                        GetDateBlock.Transaction = transaction;
                        GetDateBlock.CommandText = $"select add_months(card_exp_date,6)" +
                            $" from ecfil012@m_{filialNumber} where gr_nomer = {cardnumber}";
                        info.DateStop = (DateTime)GetDateBlock.ExecuteScalar();
                    }

                    using (OracleCommand GetOfflineServices = con.CreateCommand())
                    {
                        List<OfflineCell> services = new List<OfflineCell>();
                        GetOfflineServices.Transaction = transaction;
                        GetOfflineServices.CommandText = $"select u.nazvanie_uslugi," +
                            $" decode(k.id_schemy,3,'ЛС',5,'ЕГ',8,'ЛПЦТ','невідомо')," +
                            $" k.RAZMER_KOSHELKA, k.LIMIT," +
                            $" decode(k.PRIZNAK_ML_DIV10000,10000,'Місячний',100,'Тижневий',0,'Добовий','невідомо')," +
                            $" decode(k.INDIVIDUALNII_SL,1,'інд.',0,'заг.','невідомо')" +
                            $" from ecfil001@m_{filialNumber} u, ecfil015@m_{filialNumber} k" +
                            $" where k.id_koshelka = u.id_uslugi" +
                            $" and k.id_karty = (select id_karty from ecfil012@m_{filialNumber}" +
                            $" where gr_nomer = {cardnumber})";
                        OracleDataReader reader = GetOfflineServices.ExecuteReader();
                        while (reader.Read())
                        {
                            OfflineCell service = new OfflineCell
                            {
                                Service = reader.GetString(0),
                                Sheme = reader.GetString(1),
                                Balance = reader.GetDecimal(2),
                                Limit = reader.GetDecimal(3),
                                TermOfLimit = reader.GetString(4),
                                TypeOfLimit = reader.GetString(5)

                            };
                            services.Add(service);
                        }
                        reader.Dispose();
                        info.OfflineCells = services;
                    }

                    using (OracleCommand GetType = con.CreateCommand())
                    {
                        GetType.Transaction = transaction;
                        try
                        {
                            GetType.CommandText = $"select decode(ONLINE_CARDTYPE,1,'A',2,'B',3,'C',4,'D',6,'F','невідомо')" +
                                                $" from OC_ONL_CARDS@m_{filialNumber} where CARDNUM = {cardnumber}";
                            info.Type = GetType.ExecuteScalar().ToString();
                        }
                        catch 
                        {

                            info.Type = "A";
                        }
                    }

                    if (info.Type != "A")
                    {
                        using (OracleCommand GetBalance = con.CreateCommand())
                        {
                            GetBalance.Transaction = transaction;
                            GetBalance.CommandText = $"select BALANCE from OC_ONL_CUSTOMERS_PARAMS@m_{filialNumber}" +
                                $" where CUSTOMERS_ID = (select cln.CUSTOMERS_ID from ecfil012@m_{filialNumber} crd" +
                                $", oc_onl_customers@m_{filialNumber} cln where crd.gr_nomer = {cardnumber}" +
                                $" and cln.ID_PRINADLEJNOSTI = crd.id_prinadlejnosti" +
                                $" and cln.ID_VLADELTZA = crd.id_vladeltza)";
                            try
                            {
                                info.OnlineBalance = (decimal)GetBalance.ExecuteScalar();
                            }
                            catch
                            {
                                info.OnlineBalance = -1234567890;
                            }
                        }

                        using (OracleCommand GetOnlineServices = con.CreateCommand())
                        {
                            List<OnlineCell> onlineServices = new List<OnlineCell>();
                            GetOnlineServices.Transaction = transaction;
                            GetOnlineServices.CommandText = $"select U.NAZVANIE_USLUGI," +
                                $" OL.DAILY_LIMIT ||' / '||  case when C.CARD_EXP_DATE < trunc(sysdate)" +
                                $" then 0 else nvl(O.DAILY_CURRENT,0) end, OL.WEEKLY_LIMIT ||' / '||" +
                                $" case when C.CARD_EXP_DATE < trunc(sysdate, 'IW') then 0 else nvl(O.WEEKLY_CURRENT,0)" +
                                $" end, OL.MONTHLY_LIMIT ||' / '|| case when C.CARD_EXP_DATE < trunc(sysdate,'mm')" +
                                $" then 0 else nvl(O.MONTHLY_CURRENT,0) end from" +
                                $" oc_onl_cards_limits@m_{filialNumber} ol join ecfil012@m_{filialNumber}" +
                                $" c on Ol.CARDNUM = c.gr_nomer left join oc_onl_cards_current@m_{filialNumber}" +
                                $" o on (ol.cardnum = o.cardnum and ol.services_id = o.services_id)" +
                                $" join ecfil001@m_{filialNumber} u on (ol.services_id = U.ID_USLUGI)" +
                                $" where c.gr_nomer = {cardnumber} and ol.services_id > 0";
                            OracleDataReader reader = GetOnlineServices.ExecuteReader();
                            while (reader.Read())
                            {
                                OnlineCell service = new OnlineCell
                                {
                                    Service = reader.GetString(0),
                                    DLimit = reader.GetString(1),
                                    WLimit = reader.GetString(2),
                                    MLimit = reader.GetString(3)
                                };
                                onlineServices.Add(service);
                            }
                            reader.Dispose();
                            info.OnlineCells = onlineServices;
                        }
                    }

                    transaction.Commit();

                }
                catch
                {
                    info.Filial = "";                   
                }
            }
            return await Task.FromResult(info);
        }

        public async Task<List<CardHistoryEntry>> GetCardHistoryAsync(int cardnumber)
        {
            List<CardHistoryEntry> cardHistory = new List<CardHistoryEntry>();

            int filialNumber = 0;
            string constring = Configuration["constring_roc"];

            using (OracleConnection con = new OracleConnection(constring))
            {
                using (OracleCommand GetFilial = con.CreateCommand())
                {
                    con.Open();
                    GetFilial.CommandText = $"select filial from pc_cards" +
                        $" where card_graf_num = {cardnumber}";
                    filialNumber = Convert.ToInt32(GetFilial.ExecuteScalar());
                }
                using (OracleCommand GetHistory = con.CreateCommand())
                {
                    GetHistory.CommandText = $"select * from (select when, nvl(owner,' ')," +
                        $" decode(id_sostoyaniya,1,'В роботі',4,'Не видана',2,'В ЧС',7," +
                        $"'Фірма в ЧС',0,'Не форматована',3,'Втрачена',5,'Заблокована','Невідомо') state," +
                        $" nvl(f_i_o,' '), nvl(kommentariy,' ') from(select oc_utils_pack.to_datetime(h.data_vidachi, h.vremya_vidachi) when," +
                        $" f.name owner, h.id_sostoyaniya, h.id_who_serv, kommentariy" +
                        $"  from ecfil028 @m_{filialNumber} h left join  ecfil002@m_{filialNumber} f on h.id_vladeltza = f.id_firmy" +
                        $" where id_karty = (select id_karty from ecfil012 @m_{filialNumber}  where gr_nomer = {cardnumber})" +
                        $" and h.id_prinadlejnosti = 2 union" +
                        $" select oc_utils_pack.to_datetime(h.data_vidachi, h.vremya_vidachi)," +
                        $" f.fio, h.id_sostoyaniya, h.id_who_serv, kommentariy from ecfil028 @m_{filialNumber} h" +
                        $" left join ecfil008@m_{filialNumber} f on h.id_vladeltza = f.id_cheloveca" +
                        $" where id_karty = (select id_karty from ecfil012 @m_{filialNumber} where gr_nomer = {cardnumber})" +
                        $" and h.id_prinadlejnosti = 3)" +
                        $" left join ecfil025 @m_{filialNumber} p on id_who_serv = p.id_cheloveka  order by 1 desc) where rownum< 11";
                    OracleDataReader reader = GetHistory.ExecuteReader();
                    while (reader.Read())
                    {
                        CardHistoryEntry entry = new CardHistoryEntry
                        {
                            When = reader.GetDateTime(0),
                            Owner = reader.GetString(1),
                            State = reader.GetString(2),
                            WhoServ = reader.GetString(3),
                            Comment = reader.GetString(4)
                        };
                        cardHistory.Add(entry);
                    }
                    reader.Dispose();
                }
            }

            return await Task.FromResult(cardHistory);
        }

        public async Task<List<OPEntry>> GetOPAsync(int cardnumber)
        {
            List<OPEntry> OPList = new List<OPEntry>();

            int filialNumber = 0;
            string constring = Configuration["constring_roc"];

            try
            {
                using (OracleConnection con = new OracleConnection(constring))
                {
                    using (OracleCommand GetFilial = con.CreateCommand())
                    {
                        con.Open();
                        GetFilial.CommandText = $"select filial from pc_cards" +
                            $" where card_graf_num = {cardnumber}";
                        filialNumber = Convert.ToInt32(GetFilial.ExecuteScalar());
                    }
                    using (OracleCommand GetOP = con.CreateCommand())
                    {
                        GetOP.CommandText = $"select * from (select OC_UTILS_PACK.TO_DATETIME(op.DATA_SOZDANIA, op.VREMYA_SOZDANIA)" +
                            $" DateCreate, to_char(op.DATA_NACHALA,'dd.mm.yyyy') DateStart, to_char(op.DATA_OKONCHANIA,'dd.mm.yyyy')" +
                            $" DateEnd, u.NAZVANIE_USLUGI Service, op.SUMMA Summa, case when (op.DATA_OKONCHANIA < trunc(sysdate +1)" +
                            $" and op.sostoyanie in (0,1)) then 'Протерміновано' else" +
                            $" decode(op.SOSTOYANIE,0,'Сформовано',1,'Відправлено',2,'Реалізовано',3,'Повернено','Невідомо') end" +
                            $" as  State,  case when OP.SOSTOYANIE = 2 then oc_utils_pack.to_datetime(OP.DATA_OBSL, OP.VREMYA_OBSL)" +
                            $" else null end as DateGet from ecfil148@m_{filialNumber} op join ecfil001@m_{filialNumber} u" +
                            $" on OP.ID_KOSHELKA = U.ID_USLUGI where OP.ID_KARTI = (select id_karty from ecfil012@m_{filialNumber}" +
                            $" where gr_nomer = {cardnumber}) order by 1 desc) where rownum < 11";
                        OracleDataReader reader = GetOP.ExecuteReader();
                        while (reader.Read())
                        {
                            OPEntry entry = new OPEntry
                            {
                                CreateDate = reader.GetDateTime(0),
                                StartDate = DateTime.ParseExact(reader.GetString(1), "dd.MM.yyyy", null),
                                EndDate = DateTime.ParseExact(reader.GetString(2), "dd.MM.yyyy", null),
                                Service = reader.GetString(3),
                                Summa = reader.GetDecimal(4),
                                State = reader.GetString(5),
                                RcvDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                            };
                            OPList.Add(entry);
                        }
                        reader.Dispose();
                    }
                }
            }
            catch
            {

            }

            return await Task.FromResult(OPList);
        }

        public async Task<List<OILEntry>> GetOILsAsync(int cardnumber)
        {
            List<OILEntry> OILList = new List<OILEntry>();
            int filialNumber = 0;
            string constring = Configuration["constring_roc"];
            try
            {
                using (OracleConnection con = new OracleConnection(constring))
                {
                    using (OracleCommand GetFilial = con.CreateCommand())
                    {
                        con.Open();
                        GetFilial.CommandText = $"select filial from pc_cards" +
                            $" where card_graf_num = {cardnumber}";
                        filialNumber = Convert.ToInt32(GetFilial.ExecuteScalar());
                    }
                    using (OracleCommand GetOIL = con.CreateCommand())
                    {
                        GetOIL.CommandText = $"select * from (select OC_UTILS_PACK.TO_DATETIME(oil.DATA_FORMIROVANIYA," +
                            $" oil.VREMYA_FORMIROVANIYA) CreateDate, to_char(oil.DATA_NACHALA,'dd.mm.yyyy') StartDate," +
                            $" to_char(oil.DATA_OKONCHANIYA,'dd.mm.yyyy') EndDate, u.NAZVANIE_USLUGI Service, oil.SUMMA Summa," +
                            $" case when (oil.DATA_OKONCHANIYA < trunc(sysdate +1) and oil.STATE in (0,1)) then 'Протерміновано'" +
                            $" else decode(oil.STATE,0,'Сформовано',1,'Відправлено',2,'Реалізовано',3,'Видалено','Невідомо')" +
                            $" end as  State,  case when oil.STATE = 2 then oil.REALIZE_DATE else null end as RCVDate" +
                            $" from ecfil157@m_{filialNumber} oil join ecfil001@m_{filialNumber} u" +
                            $" on oil.ID_KOSHELKA = U.ID_USLUG" +
                            $"I where oil.ID_KARTI = (select id_karty from ecfil012@m_{filialNumber} where gr_nomer = {cardnumber})" +
                            $" order by 1 desc) where rownum < 11";
                        OracleDataReader reader = GetOIL.ExecuteReader();
                        while (reader.Read())
                        {
                            OILEntry entry = new OILEntry
                            {
                                CreateDate = reader.GetDateTime(0),
                                StartDate = DateTime.ParseExact(reader.GetString(1), "dd.MM.yyyy", null),
                                EndDate = DateTime.ParseExact(reader.GetString(2), "dd.MM.yyyy", null),
                                Service = reader.GetString(3),
                                Summa = reader.GetDecimal(4),
                                State = reader.GetString(5),
                                RcvDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                            };
                            OILList.Add(entry);
                        }
                        reader.Dispose();
                    }
                }
                
            }
            catch
            {

            }
            return await Task.FromResult(OILList);
        }

        public async Task<List<Tranz>> GetTranzsAsync(int cardnumber, DateTime startdate)
        {
            List<Tranz> tranzactions = new List<Tranz>();
            string constring = Configuration["constring_roc"];
            string datestring = startdate.ToString("dd.MM.yyyy");

            try
            {
                using (OracleConnection con = new OracleConnection(constring))
                {
                    using (OracleCommand GetTranz = con.CreateCommand())
                    {
                        con.Open();
                        GetTranz.CommandText = $"select date_of, date_in, SERVICE_NAME, service_amount, pos_emitent," +
                            $" pos_number_local, NAZVANIYE, ADRES, decode(id_operation,29,'Обслуг.','Поверн.')" +
                            $" from pc_transactions p_t, pc_pos p_p, pc_glob_service g_s" +
                            $" where p_t.id_service_for=g_s.ID_GLOB_SERVICE and p_t.pos_number_global=p_p.pos_number_global" +
                            $" and card_graf_num = {cardnumber} and date_of > to_date('{datestring}','dd.mm.yyyy') and id_operation in (29,30,31)" +
                            $" order by date_of";
                        OracleDataReader reader = GetTranz.ExecuteReader();
                        while (reader.Read())
                        {
                            Tranz tranz = new Tranz
                            {
                                DateOf = reader.GetDateTime(0),
                                DateIn = reader.GetDateTime(1),
                                Service = reader.GetString(2),
                                Amount = reader.GetDecimal(3),
                                PosEmitent = reader.GetInt32(4),
                                TONum = reader.GetInt32(5),
                                TOName = reader.GetString(6),
                                TOAddres = reader.GetString(7),
                                TypeOfServ = reader.GetString(8)
                            };
                            tranzactions.Add(tranz);
                        }
                        reader.Dispose();
                    }
                }
            }
            catch
            {

            }

            return await Task.FromResult(tranzactions);
        }
        
    }
}

