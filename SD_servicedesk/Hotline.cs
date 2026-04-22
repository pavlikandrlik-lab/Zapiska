using DataLayer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static DataLayer.Hotline;
using static DataLayer.Hotline.Model;
using static System.Net.Mime.MediaTypeNames;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using BusinessLayer.GridBusiness;
using BusinessLayer.GlobalModel;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using OfficeOpenXml;
using Infrastructure;
using System.Runtime.Remoting.Contexts;
using System.Data.Entity.Core.Metadata.Edm;
using System.Runtime.InteropServices.ComTypes;
using System.Drawing;

namespace BusinessLayer
{
    public class Hotline
    {
        string user = null;
        Permissions perm = null;
        public Hotline(string u, Permissions p)
        {
            if (string.IsNullOrEmpty(u)) throw new Exception("UserEmpty");
            user = u;
            perm = p;

        }
        internal Hotline() { }
            /// <summary>
            /// Vypíše seznam tiketů uživatele dle jeho zařazení
            /// </summary>
            /// <param name="user"></param>
            /// <returns>Seznam záznamů</returns>
            public IEnumerable<Model.Ticket> GetTicketsByUser(int limit = 100)
        {
            var connect = new Data();
            var ISModel = connect.GetISModel();
            var TymModel = connect.GetTymy();
            string where = "WHERE " + ApproveUserToTicket(ISModel, TymModel);
            var data = connect.GetTickets(where, new List<string>() { "ID" }, "DESC", limit);
            data = WhyIsItemsVisible(data.ToList(), ISModel, TymModel);
            return data;
        }
        public IEnumerable<Model.Ticket> GetTicketsByUserExt(ref GridParams gp, out List<string> subsystem, out List<string> modul, out List<string> infsys, bool listed = true)
        {
            try
            {
                var connect = new Data();
                var ISModel = connect.GetISModel(); 
                Dictionary<string, string> ISModelTiny = ISModel.SelectMany(q => q.subsystems.Select(p => new { q, p })).ToDictionary(t => t.p.zkratka, t => t.q.zkratka);
                var ISModelStr = ISModel.SelectMany(q => q.subsystems.Select(p => new { q, p })).Select(t => t.p.zkratka + "=" + t.q.zkratka).ToList();
                var TymModel = connect.GetTymy();
                infsys = ISModel.Select(q => q.zkratka).ToList();
                using (HotlineEntities hotline = new HotlineEntities()) {
                    var data = connect.GetTicketsData(hotline, ISModelStr);
                    data = data.ApproveUserToTickets(user, ISModel, TymModel, SubtractFilteredPermissions(perm, gp));
                   
                    data = data.FilterColumn(gp);
                    var result = WhyIsItemsVisible(data.ToList(), ISModel, TymModel);
                    data = FilterColumnUserVisible(result, gp).AsQueryable();
                    
                    gp.TotalRows = data.Count();
                    modul = data.GetModul();
                    subsystem = data.GetSubsystem();
                    if (listed)
                    {
                        double? offset = Helper.DivideRoundUp(gp.TotalRows, gp.RowsPerPage); //(double?)gp.TotalRows / gp.RowsPerPage;
                        if (gp.CurrentPage > offset)
                        {
                            gp.CurrentPage = 1;
                        }
                        result = data.PageList(gp, "Id", true);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                gp.TotalRows = 0;
                gp.CurrentPage = 1;
                subsystem = null;
                modul = null;
                infsys = null;
                return null;
            }
        }

        internal Model.Ticket GetTicketByID(int ticketId, bool loadSLAInfo = true)
        {
            var connect = new Data();
            string where = "WHERE id='" + ticketId + "'";
            var data = connect.GetTicketByID(where, ticketId, loadSLAInfo);
            return data;
        }
        public Dictionary<string, string> GetSearchResults(string query)
        {
            var connect = new Data();
            string where = "WHERE id LIKE '%" + query + "%'";
            var data = connect.GetTicketsCompact(where, 5);
            var list = data.ToDictionary(q => q.Id.ToString(), q => q.Strucne);
            return list;
        }
        private Permissions SubtractFilteredPermissions(Permissions perm, GridParams gp)
        {
            var newPerm = new Permissions(perm.Name);
            var procToVidimFilter = gp.Filters.Where(q => q.Name == "ProcToVidim" && q.Value.Count() > 0);
            if (procToVidimFilter.Count() > 0)
            {
                if (procToVidimFilter.First().Type == "equals")
                {
                    var keepItem = perm.Permission.Where(q => q.Name == procToVidimFilter.First().Value.First()).First();
                    newPerm.Permission.Add(keepItem);
                }
                else if (procToVidimFilter.First().Type == "not-equals") {
                    foreach(var p in perm.Permission.Where(q => q.Name != procToVidimFilter.First().Value.First()))
                    {
                        newPerm.Permission.Add(p);
                    }
                }
            } else
            {
                newPerm.Permission = perm.Permission;
            }
            return newPerm;
        }

        public List<Model.Stats> GetTicketsStatsByUser()
        {

            var connect = new Data();
            var ISModel = connect.GetISModel();
            var TymModel = connect.GetTymy();
            string where = "WHERE (" + ApproveUserToTicket(ISModel, TymModel) + ") AND  datum > DATEADD(year,-1,GETDATE()) ";
            var data = connect.GetTicketsCompact(where, 40000);
            data = WhyIsItemsVisible(data.ToList(), ISModel, TymModel);

            var stats = new List<Model.Stats>();

            stats = stats.GetStatsOpenedTickets(data).GetStatsDodavatelTickets(data);

            return stats;
        }

        public Model.Ticket GetTicketByIDByUser(int ticketId, bool loadSLAInfo = true)
        {
            var connect = new Data();
            var ISModel = connect.GetISModel();
            var TymModel = connect.GetTymy();
            var userLogin = connect.GetUsersLoginByName();
            string where = "WHERE (" + ApproveUserToTicket(ISModel, TymModel) + ") AND  id='" + ticketId + "'";
            var data = connect.GetTicketByID(where, ticketId, loadSLAInfo);
            if (data != null)
            {
                foreach (var c in data.Comments)
                {
                    var zpracoval = userLogin.Where(q => q.uzivatel == c.Zpracoval).ToList();
                    if (zpracoval.Count() == 1)
                    {
                        if (zpracoval.First().login.ToLower() == user.ToLower())
                        {
                            c.IsUserComment = true;
                        }
                    }
                    c.Popis = HotlineURlReplacement(connect, data, c.Popis);
                }
                data.Popis = HotlineURlReplacement(connect, data, data.Popis);             
                data = WhyIsItemVisible(data, ISModel, TymModel);
            }
            return data;
        }


        private string ApproveUserToTicket(List<ISModel> ISModel, List<Tymy> TymModel)
        {

            // načíst tickety, kde je uživatel jako zadavatel
            string where = "";
            if (perm.Permission.Any(q => q.Name == "ZP"))
            {
                where = where.LinqAddOr("zal_HFU='" + user.ToLower() + "'");
            }
            // kde je uživatel jako VS
            foreach (var p in perm.Permission.Where(q => q.Name == "VS"))
            {
                foreach (var val in p.Values)
                {
                    where = where.LinqAddOr("subsystem='" + val + "'");
                }
            }

            // kde je uživatel jako člen VFIS (FIS/ISSP)
            foreach (var p in perm.Permission.Where(q => q.Name == "PM"))
            {
                foreach (var val in p.Values)
                {
                    var ms = ISModel.Where(q => q.zkratka == val);
                    foreach (var m in ms)
                    {
                        foreach (var q in m.subsystems)
                        {
                            where = where.LinqAddOr("subsystem='" + q.zkratka + "'");
                        }
                    }
                }
            }

            // kde je uživatel jako člen ŘT
            foreach (var p in perm.Permission.Where(q => q.Name == "RT"))
            {
                foreach (var val in p.Values)
                {
                    var tm = TymModel.Where(q => q.nazev_z == val);
                    foreach (var t in tm)
                    {
                        where = where.LinqAddOr("subsystem='" + t.subsystem + "'");
                    }
                }
            }
            if (perm.Permission.Any(q => q.Name == "ZP"))
            {
                where = where.LinqAddOr("1=1");
            }

            where = where.LinqAddOr("1=2");

            // * možná doplnit, kde je uživatel jako člen výše uvedených skupin ve formě komentáře

            return where;
        }

        private IEnumerable<Model.Ticket> WhyIsItemsVisible(List<Model.Ticket> data, List<ISModel> ISModel, List<Tymy> TymModel)
        {

            for (var i = 0; i < data.Count(); i++)
            {
                data[i] = WhyIsItemVisible(data[i], ISModel, TymModel);
            }

            return data;
        }
        private Model.Ticket WhyIsItemVisible(Model.Ticket item, List<ISModel> ISModel, List<Tymy> TymModel)
        {
            if (item.Perm == null) item.Perm = new List<TicketPermission>(); // Dictionary<string, string>();
            if (item.ZalozilLogin != null)
            {
                if (item.ZalozilLogin.ToLower() == user.ToLower())
                {
                    item.Perm.Add(new TicketPermission() { Key = "ZP", UserText = "ZADAVATEL: " + item.Zpracoval, TymText = "ZADAVATEL" });
                }
            }
            foreach (var p in perm.Permission.Where(q => q.Name == "VS"))
            {
                foreach (var val in p.Values)
                {
                    var ms = ISModel.Where(q => q.subsystems.Any(z => z.zkratka == val));
                    if (val == item.Subsystem)
                    {
                        string tymtext = val;
                        if (ms.Count() == 1) { tymtext = ms.First().zkratka; }
                        item.Perm.Add(new TicketPermission() { Key = "VS", UserText = "Vedoucí subsystému: " + val, TymText = "VS " + tymtext });
                    }
                }
            }
            foreach (var p in perm.Permission.Where(q => q.Name == "PM"))
            {
                foreach (var val in p.Values)
                {
                    var ms = ISModel.Where(q => q.zkratka == val);
                    foreach (var m in ms)
                    {
                        foreach (var q in m.subsystems)
                        {
                            if (item.Subsystem == q.zkratka)
                            {
                                item.Perm.Add(new TicketPermission() { Key = "PM", UserText = "Projektový manažer: " + val, TymText = "Projektový manažer - " + val });

                            }
                        }
                    }
                }
            }

            foreach (var p in perm.Permission.Where(q => q.Name == "RT"))
            {
                foreach (var val in p.Values)
                {
                    var tm = TymModel.Where(q => q.nazev_z == val);
                    foreach (var t in tm)
                    {
                        if (t.subsystem == item.Subsystem)
                        {
                            item.Perm.Add(new TicketPermission() { Key = "RT", UserText = "Řešitelský tým: " + val + " - " + t.subsystem, TymText = t.nazev_z });
                        }
                    }
                }
            }
            item.Perm.Add(new TicketPermission() { Key = "EO", UserText = "Pozorovatel", TymText = "Pozorovatel" });
            
            return item;
        }

        /// <summary>
        /// speciální filtr na sloupec proctovidim, který se má odfiltrovat poté, co proběhnou všechny filty a donačtou se informace o uživateli a pro daný řádek vidí
        /// </summary>
        /// <param name="result"></param>
        /// <param name="gp"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private IEnumerable<Ticket> FilterColumnUserVisible(IEnumerable<Ticket> result, GridParams gp)
        {
            if (gp.Filters.Any(q => q.Name == "ProcToVidim"))
            {
                var item = gp.Filters.Where(q => q.Name == "ProcToVidim").ToList()[0];
                if (item.Type == "not-equals")
                {
                    return result.Where(q => !item.Value.Contains(q.Perm.Select(x => x.Key).FirstOrDefault()));
                  //  return result.Where(q => !q.Perm.Any(z => z.Key == item.Value[0]));
                }
                else if (item.Type == "equals")
                {
                    return result.Where(q => q.Perm.Any(z => z.Key == item.Value[0]));
                }
                else
                {
                    return result;

                }
            }
            {
                return result;
            }
        }


        public static string HotlineURlReplacement(Data connect, Ticket ticket, string input)
        {
            // najít <a> a konec </a> a nahradit odkaz, aktuálně nahrazuje odkazy křížové na jiné hotline, pokud existují v synchronizační databázi, dále nahrazuje odkazy na  
            int offset = 0;
            string result = input;
            while (true)
            {
                string starttag = "<a ";
                string endtag = "</a>";
                int poziceStartTag = input.ToLower().IndexOf(starttag, offset);
                if (poziceStartTag == -1)
                {
                    offset = input.Length;
                    break;
                }
                else
                {
                    int poziceEndTag = input.ToLower().IndexOf(">", poziceStartTag) + 1;
                    int poziceEndFinal = input.ToLower().IndexOf(endtag, poziceEndTag);
                    if (poziceEndFinal == -1 | poziceEndTag == -1)
                    {
                        offset = input.Length;
                        break;
                    }
                    else
                    {
                        if (poziceEndTag < input.Length && poziceEndFinal < input.Length)
                        {
                            string ahref = input.Substring(poziceStartTag, poziceEndTag - poziceStartTag);

                            string pattern = @"href=[""']?([^""' >]+)";

                            string href = "";
                            Match match = Regex.Match(ahref.ToLower(), pattern);
                            if (!match.Success)
                            {
                            }
                            else
                            {
                                href = match.Groups[1].Value;

                                string content = input.Substring(poziceEndTag, poziceEndFinal - poziceEndTag);

                                string aToReplace = input.Substring(poziceStartTag, poziceEndFinal - poziceStartTag + endtag.Length);
                                string crossLinkA = Settings.Read("HotlineCrossLinkA"), crossLinkB = Settings.Read("HotlineCrossLinkB"), crossLinkC = Settings.Read("HotlineCrossLinkC");
                                string aNew = "";


                                // klasický odkaz obsahující PID
                                if (href.ToLower().Contains(crossLinkA.ToLower()))
                                {

                                    int pos = href.ToLower().IndexOf(crossLinkA.ToLower()) + crossLinkA.Length;
                                    string pid = href.Substring(pos);

                                    string link = connect.GetTicketIDByPID(pid);

                                    if (link == "")
                                    {
                                        aNew = content;
                                    }
                                    else
                                    {
                                        aNew = "<a href='/Hotline/Ticket/Details/" + link + "'>" + content + "</a>";
                                    }
                                }
                                // odkaz na přílohu z prilohy/
                                else if (href.ToLower().Contains(crossLinkB.ToLower()))
                                {
                                    int pos = href.ToLower().IndexOf(crossLinkB.ToLower()) + crossLinkB.Length;
                                    string fileName = href.Substring(pos);
                                    var attchs = ticket.Attachments.Where(q => q.FileName.ToLower() == fileName);

                                    string link = "";
                                    if (attchs.Count() == 1)
                                    {
                                        link = attchs.First().Id + "_" + attchs.First().FileName;
                                    }
                                    if (link == "")
                                    {
                                        aNew = content;
                                    }
                                    else
                                    {
                                        aNew = "<a href='/Hotline/Ticket/GetAttachment/" + ticket.Id + "?att=" + link + "'>" + content + "</a>";
                                    }

                                }
                                // okdza na přílohu z vyjadreni_prilohy/
                                else if (href.ToLower().Contains(crossLinkC.ToLower()))
                                {
                                    int pos = href.ToLower().IndexOf(crossLinkC.ToLower()) + crossLinkC.Length;
                                    string fileName = href.Substring(pos);
                                    var attchs = ticket.Attachments.Where(q => q.FileName.ToLower() == fileName);

                                    string link = "";
                                    if (attchs.Count() == 1)
                                    {
                                        link = attchs.First().Id + "_" + attchs.First().FileName;
                                    }
                                    if (link == "")
                                    {
                                        aNew = content;
                                    }
                                    else
                                    {
                                        aNew = "<a href='/Hotline/Ticket/GetAttachment/" + ticket.Id + "?att=" + link + "'>" + content + "</a>";
                                    }

                                }
                                else
                                {
                                    aNew = "" + content + " <i>(nefunkční odkaz)</i>";
                                }

                                result = result.Replace(aToReplace, aNew);
                            }
                        }
                        offset = poziceEndFinal + 3;
                    }
                }
            }
            return result;
        }
        public byte[] GetAttachment(int id, string key, ref string filename)
        {
            try
            {
                var t = GetTicketByIDByUser(id);
                var atts = t.Attachments.Where(q => key == q.Id + "_" + q.FileName);
                if (atts.Count() != 1)
                {
                    filename = null;
                    return null;
                }
                else
                {
                    var att = atts.First();
                    byte[] b = System.IO.File.ReadAllBytes(att.FilePath + att.FileName);
                    filename = att.Nazev;
                    return b;
                }
            } catch (Exception ex)
            {
                filename = null;
                return null;
            }

        }

        public string CreateAttachmentResult(int id, string perm, List<AttachmentUpload> fileData, string userLogin)
        {
            JSONResult res = CreateAttachment(id, perm, fileData, userLogin);
            UploadState data = new UploadState { text = res.Output, state = res.State };
            string output = JsonConvert.SerializeObject(data);
            return output;
        }

        public JSONResult CreateAttachment(int id, string perm, List<AttachmentUpload> fileData, string userLogin)
        {
            JSONResult res = new JSONResult();
            try
            {
                string fileDestination = Settings.Read("HotlineAttachmentPathSourceK");
                var ticket = GetTicketByIDByUser(id);
                if (ticket == null)
                {
                    res.Output = "ERROR: Nemáte oprávnění k tiketu.";
                    res.State = false;
                }
                else
                {
                    if (!CheckPermissionForUseRole(ticket, perm))
                    {
                        // uzivatel nema opravneni zadat komentar dle vyberu, ktery zvolil

                        res.Output = "ERROR: Nemáte oprávnění nahrát přílohu pod komentářem, který jste si zvolili.";
                        res.State = false;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(ticket.PID))
                        {
                            // chyba cteni pidu

                            res.Output = "ERROR: Nepodařilo se nahrát přílohy.";
                            res.State = false;
                        }
                        else
                        {
                            string userName = GetUserNameFromHotlinebyLogin(userLogin);

                            // zjistit nové pojmenování příloh
                            CreateFileNames(ref fileData, ticket, fileDestination);

                            // uloži třílohy na server
                            SaveFilesToStorage(ref fileData, fileDestination);

                            // zapsat přílohy do databáze
                            SaveFilesToDB(ref fileData, ticket, userName, perm);

                            // vytvořit komentář v databázi
                            res.Output = CreateTechComment(ref fileData, ticket, userName, perm, out bool state);

                            // Načíst si znovu tikcet
                            ticket = GetTicketByIDByUser(id);

                            // nahradit původní url adresy novými odkazy pro servicdesk
                            res.Output = HotlineURlReplacement(new Data(), ticket, res.Output);
                            if (state == false)
                            {
                                res.Output = "ERROR: Nepodařilo se zapsat komentář s přílohami. Prosím prověřte stav na kartě přílohy a případně kontaktujte HelpDesk FIS.";
                            }
                            res.State = state;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                res.State = false; res.Output = "Nespecifikovaná chyba! "  + ex.Message;
            }
            // odeslat zpět info

            return res;
        }

        private bool CheckPermissionForUseRole(Ticket ticket, string perm)
        {
            return ticket.Perm.Select(q => q.TymText).Contains(perm) ? true : false;
        }

        private string CreateTechComment(ref List<AttachmentUpload> fileData, Ticket ticket, string userName, string tym, out bool state)
        {
            var connect = new Data();
            string url = Settings.Read("HotlinePrilohyVyjadreniURL");

            string text = ((fileData.Count() == 1) ? "Byla připojena příloha:<br/>" : "Byly připojeny přílohy:<br/><ul>");
            foreach (var file in fileData.Where(q => q.DatabaseState == true)) {
                text += "<li><a href='" + url + file.NewFileName + "' target='_HOTLINE'><strong>" + file.FileName + "</strong></a></li>";
            }
            text += "</ul>";
            if (fileData.Where(q => q.DatabaseState == false).Count() != 0)
            {
                text += "<br/>Tyto soubory se nepodařilo nahrát:<br/><ul>";
                foreach (var file in fileData.Where(q => q.DatabaseState == false))
                {
                    text += "<li><strong>" + file.FileName + "</strong></li>";
                }
                text += "</ul>";
            }
            int idResult = connect.CreateComment(ticket.PID, userName, text, tym, "Z", false);
            if (idResult == -1)
            {
                state = false;
            }
            else
            {
                state = true;
            }
            return text;
        }

        private string GetUserNameFromHotlinebyLogin(string login)
        {
            var connect = new Data();
            return connect.GetUserNameByLogin(login);
        }

        private void SaveFilesToDB(ref List<AttachmentUpload> fileData, Ticket ticket, string userName, string tym)
        {
            var connect = new Data();
            foreach (var file in fileData)
            {
                if (file.StorageState) {
                    int idResult = connect.CreateComment(ticket.PID, userName, file.NewFileName, tym, "P", false);
                    if (idResult != -1)
                    {
                        file.DatabaseState = true;
                    }
                }
            }
        }

        private void SaveFilesToStorage(ref List<AttachmentUpload> fileData, string fileDestination)
        {
            foreach (var file in fileData)
            {
                using (var fileStream = File.Create(fileDestination + file.NewFileName))
                {
                    file.Data.Seek(0, SeekOrigin.Begin);
                    file.Data.CopyTo(fileStream);
                    file.StorageState = true;
                }
            }
        }

        private void CreateFileNames(ref List<AttachmentUpload> fileData, Model.Ticket ticket, string fileDestination)
        {
            foreach (var file in fileData)
            {
                string newFileName = "";
                while (true)
                {
                    newFileName = ticket.PID + "_" + BusinessLayer.Helper.RandomString(8) + Path.GetExtension(fileDestination + file.FileName);
                    if (!ticket.Attachments.Select(q => q.FileName).Contains(newFileName) && !fileData.Select(q => q.NewFileName).Contains(newFileName))
                    {
                        file.NewFileName = newFileName;
                        break;
                    }
                }
            }
        }

        public string CreateCommentResult(int id, string perm, string comment, string userLogin)
        {

            JSONResult res = CreateComment(id, perm, comment, userLogin);
            UploadState data = new UploadState { text = res.Output, state = res.State };
            string output = JsonConvert.SerializeObject(data);
            return output;
        }
        public JSONResult CreateComment(int id, string perm, string comment, string userLogin)
        {
            JSONResult res = new JSONResult();
            try
            {

                var ticket = GetTicketByIDByUser(id);
                if (ticket == null)
                {
                    res.Output = "ERROR: Nemáte oprávnění k tiketu.";
                    res.State = false;
                }
                else
                {
                    if (!CheckPermissionForUseRole(ticket, perm))
                    {
                        // uzivatel nema opravneni zadat komentar dle vyberu, ktery zvolil

                        res.Output = "ERROR: Nemáte oprávnění nahrát přílohu pod komentářem, který jste si zvolili.";
                        res.State = false;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(ticket.PID))
                        {
                            // chyba cteni pidu

                            res.Output = "ERROR: Nepodařilo se nahrát přílohy.";
                            res.State = false;
                        }
                        else
                        {
                            string userName = GetUserNameFromHotlinebyLogin(userLogin);

                            // vytvořit komentář v databázi
                            res.Output = CreateUserComment(comment, ticket, userName, perm, out bool state);

                            // nahradit původní url adresy novými odkazy pro servicdesk
                            if (state == false)
                            {
                                res.Output = "ERROR: Nepodařilo se zapsat komentář. Prosím prověřte stav na kartě vyjádření a případně kontaktujte HelpDesk FIS.";
                            }
                            res.State = state;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                res.State = false; res.Output = "Nespecifikovaná chyba!";
            }
            // odeslat zpět info
            return res;
        }
        private string CreateUserComment(string text, Ticket ticket, string userName, string tym, out bool state)
        {
            var connect = new Data();
            int idResult = connect.CreateComment(ticket.PID, userName, text, tym, "Z", false);
            if (idResult == -1)
            {
                state = false;
            }
            else
            {
                state = true;
            }
            return text;
        }

        public string UpdateTerminSplneni(int idTicket, string terminSplneni, string login, out bool state)
        {
            string output = "";
            try
            {
                DateTime ts;
                if (DateTime.TryParse(terminSplneni, out ts))
                {
                    var ticket = GetTicketByIDByUser(idTicket);
                    var ticketPerm = ticket.Perm.Where(q => q.Key == "PM");
                    if (ticketPerm.Select(q => q.Key).Any(x => perm.Permission.Select(q => q.Name).Contains(x)))
                    {
                        var connect = new Data();
                        string userName = connect.GetUserNameByLogin(login);

                        string text = "#schvalenizmenyterminu <br>" +userName + " jako " + ticketPerm.First().TymText + " povolil změnu termínu dodání/vyřešení tohoto záznamu (" + ticket.TypZaznamu + ") řešeného firmou " + ticket.Dodavatel + " na " + ts.ToString("dd.MM.yyyy") + ".";
                        string tym = ticketPerm.First().TymText;

                        int idResult = connect.EditTicketHeaderDatum(idTicket, "dat_res_t", ts);
                        if (idResult != 1)
                        {
                            state = false;
                            output = "Nepodařilo se zapsat změny.";
                        }
                        else
                        {
                            int idResultComment = connect.CreateComment(ticket.PID, userName, text, tym, "Z", false);
                            if (idResultComment == -1)
                            {
                                state = false;
                                output = "Nepodařilo se zapsat změny.";
                            }
                            else
                            {

                                state = true;
                                output = "Změna provedena.";
                                /*
                                 * Není aktuálně potřeba, vyřešeno úpravou konektoru 
                                var k = new Konektor();
                                int idResultKonektorComment = k.UpdateDatResTym(ts, ticket.Id);
                                if (idResultKonektorComment == -1)
                                {
                                    state = false;
                                    output = "Změna zapsána do Hotline. Termín k dodavateli se povedlo zapsat jen částečně.";
                                }
                                else
                                {
                                    state = true;
                                    output = "Změna provedena.";
                                }*/
                            }
                        }
                    }
                    else
                    {
                        state = true;
                        output = "Nemáte oprávnění k provedení změny.";
                    }
                }
                else
                {
                    state = false;
                    output = "Nesprávný formát data.";
                }
            }
            catch (Exception ex)
            {
                state = false;
                output = "Systémová chyba.";
            }
            return output;
        }

        public List<GlModel.Ciselnik> GetCountersGrid(List<string> subsystem, List<string> modul, Permissions procToVidim, List<string> infsys)
        {
            List<GlModel.Ciselnik> c = new List<GlModel.Ciselnik>();
            c.Add(GetCounterTyp());
            c.Add(GetCounterStav());
            c.Add(GetCounterDnyKReseni());
            c.Add(GetCounterHodinyKReseni());
            c.Add(GetCounterProcToVidim());
            c.Add(GetCounterBasedOnList("infsys", infsys));
            c.Add(GetCounterBasedOnList("subsystem", subsystem));
            c.Add(GetCounterBasedOnList("modul", modul));
            c.Add(GetCounterBasedOnList("perm", modul)); // procToVidim.Permission.Select(q => q.Name).ToList()); ; );
            return c;
        }

        private GlModel.Ciselnik GetCounterDnyKReseni()
        {
            List<GlModel.SelectListItem> ctyp = new List<GlModel.SelectListItem>(){
                new GlModel.SelectListItem() { Text = "V pořádku", Value = "greather_then_5" },
                new GlModel.SelectListItem() { Text = "Blíží se prodlení", Value = "less_then_6:greather_then_-1" },
                new GlModel.SelectListItem() { Text = "V prodlení", Value = "less_then_0" }
            };
            return new GlModel.Ciselnik()
            {
                Name = "dnykreseni",
                Item = ctyp
            };            
        }

        private GlModel.Ciselnik GetCounterHodinyKReseni()
        {
            List<GlModel.SelectListItem> ctyp = new List<GlModel.SelectListItem>(){
                new GlModel.SelectListItem() { Text = "V pořádku (>48h)", Value = "greather_then_48" },
                new GlModel.SelectListItem() { Text = "Blíží se prodlení (48-0h)", Value = "less_then_49:greather_then_-1" },
                new GlModel.SelectListItem() { Text = "V prodlení (<0h)", Value = "less_then_0" }
            };
            return new GlModel.Ciselnik()
            {
                Name = "hodinykreseni",
                Item = ctyp
            };
        }
        private GlModel.Ciselnik GetCounterBasedOnList(string v, List<string> data)
        {
            List<GlModel.SelectListItem> citem = new List<GlModel.SelectListItem>();
            if(data != null) {
            foreach (string val in data)
            {
                citem.Add(new GlModel.SelectListItem() { Text = val, Value = val });
                }
            }
            return new GlModel.Ciselnik()
            {
                Name = v,
                Item = citem
            };
        }

        private GlModel.Ciselnik GetCounterStav()
        {
            List<GlModel.SelectListItem> cstav = new List<GlModel.SelectListItem>(){
                new GlModel.SelectListItem() { Text = "Otevřeno", Value = "otevřeno" },
                new GlModel.SelectListItem() { Text = "Dodavatel", Value = "dodavatel" },
                new GlModel.SelectListItem() { Text = "Od dodavatele", Value = "od dodavatele" },
                new GlModel.SelectListItem() { Text = "K dodavateli", Value = "k dodavateli" },
                new GlModel.SelectListItem() { Text = "Archiv", Value = "archiv" }
            };
            return new GlModel.Ciselnik()
            {
                Name = "stav",
                Item = cstav
            };
        }

        private GlModel.Ciselnik GetCounterTyp()
        {
            List<GlModel.SelectListItem> ctyp = new List<GlModel.SelectListItem>(){
                new GlModel.SelectListItem() { Text = "NES", Value = "NES" },
                new GlModel.SelectListItem() { Text = "PMP", Value = "PMP" },
                new GlModel.SelectListItem() { Text = "PNF", Value = "PNF" }
            };
            return new GlModel.Ciselnik()
            {
                Name = "typ",
                Item = ctyp
            };
        }

        private GlModel.Ciselnik GetCounterProcToVidim()
        {
            List<GlModel.SelectListItem> permissions = new List<GlModel.SelectListItem>();
            permissions.Add(new GlModel.SelectListItem() { Text = "Zadavatel", Value =  "ZP" });
            permissions.Add(new GlModel.SelectListItem() { Text = "VS FIS", Value =  "VS" });
            permissions.Add(new GlModel.SelectListItem() { Text = "Řešitelský tým", Value =  "RT" });
            permissions.Add(new GlModel.SelectListItem() { Text = "PM FIS", Value = "PM" });
            permissions.Add(new GlModel.SelectListItem() { Text = "Pozorovatel", Value = "EO" });
            return new GlModel.Ciselnik()
            {
                Name = "proctovidim",
                Item = permissions
            };
        }

        public Stream GetListOfTicketsToExcel(IEnumerable<Ticket> tickets, string user)
        {

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var ms = new MemoryStream();
                using (var p = new ExcelPackage(ms))
                {
                    var sheet = p.Workbook.Worksheets.Add("Seznam");

                    var rowIndex = 1;
                    sheet.Cells[rowIndex, 1].Value = "HTL";
                    sheet.Cells[rowIndex, 2].Value = "Typ záznamu";
                    sheet.Cells[rowIndex, 3].Value = "Subsystém";
                    sheet.Cells[rowIndex, 4].Value = "Modul";
                    sheet.Cells[rowIndex, 5].Value = "Popis";
                    sheet.Cells[rowIndex, 6].Value = "Datum";
                    sheet.Cells[rowIndex, 7].Value = "Zpracoval";
                    sheet.Cells[rowIndex, 8].Value = "Stav";
                    sheet.Cells[rowIndex, 9].Value = "Tým";
                    sheet.Cells[rowIndex, 10].Value = "Termín týmu";
                    sheet.Cells[rowIndex, 11].Value = "Termín SLA";
                    sheet.Cells[rowIndex, 12].Value = "Dní po termínu týmu/SLA";
                    sheet.Cells[rowIndex, 13].Value = "Typ prodlení";
                    sheet.Cells[rowIndex, 14].Value = "Kategorie";

                rowIndex++;
                foreach (var row in tickets)
                {
                    sheet.Cells[rowIndex, 1].Value = row.Id;
                    sheet.Cells[rowIndex, 2].Value = row.TypZaznamu;
                    sheet.Cells[rowIndex, 3].Value = row.Subsystem;
                    sheet.Cells[rowIndex, 4].Value = row.Modul;
                    sheet.Cells[rowIndex, 5].Value = row.Strucne;
                    if (row.Datum != null)
                    {
                        sheet.Cells[rowIndex, 6].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.Datum);
                        sheet.Cells[rowIndex, 6].Style.Numberformat.Format = "mm-dd-yy";
                    }
                    sheet.Cells[rowIndex, 7].Value = row.Zpracoval;
                    sheet.Cells[rowIndex, 8].Value = row.Stav;
                    sheet.Cells[rowIndex, 9].Value = row.ResTym;


                    if (row.DatResTym != null)
                    {
                        sheet.Cells[rowIndex, 10].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.DatResTym);
                        sheet.Cells[rowIndex, 10].Style.Numberformat.Format = "mm-dd-yy";
                    }

                    if (row.SLADeadline != null && row.Stav == "dodavatel")
                    {
                        sheet.Cells[rowIndex, 11].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.SLADeadline);
                        sheet.Cells[rowIndex, 11].Style.Numberformat.Format = "mm-dd-yy";
                    }

                    if (row.SLADeadline != null && row.Stav == "dodavatel")
                    {
                        double days = (Math.Floor(MOSD_External.CalculateWorkdays((DateTime)row.SLADeadline, DateTime.Now).TotalDays));
                        sheet.Cells[rowIndex, 12].Value = (days > 0) ? Math.Abs(days).ToString() : "";
                        sheet.Cells[rowIndex, 13].Value = (days > 0) ? "SLA" : "";
                    }
                    else if (row.DatResTym != null)
                    {
                        double days = Math.Floor((DateTime.Now - (DateTime)row.DatResTym).TotalDays);
                        sheet.Cells[rowIndex, 12].Value = (days > 0) ? Math.Abs(days).ToString() : "";
                        sheet.Cells[rowIndex, 13].Value = (days > 0) ? "TT" : "";
                    }
                    sheet.Cells[rowIndex, 14].Value = row.Dulezitost;

                    rowIndex++;
                }
                    sheet.Cells.AutoFitColumns();

                p.Workbook.Properties.Title = "Sestava záznamů Hotline (ServiceDesk)";
                p.Workbook.Properties.LastModifiedBy = user;
                p.Workbook.Properties.Author = "ServiceDesk FIS - " + user;
                p.Workbook.Properties.Application = "ServiceDesk FIS";
                p.Workbook.Properties.Company = "SE MO a AF";
                p.Save();
                }
            ms.Position = 0;
            return ms;
        }

        public IEnumerable<Ticket> GetTicketsSyncedWithSupplier()
        {
            var connect = new Data();
            var ISModel = connect.GetISModel();
            var ISModelStr = ISModel.SelectMany(q => q.subsystems.Select(p => new { q, p })).Select(t => t.p.zkratka + "=" + t.q.zkratka).ToList();
            var externalTickets = MOSD_External.GetSyncedTickets();

            List<Ticket> tckComplete = new List<Ticket>();
            var TymModel = connect.GetTymy();
            using (HotlineEntities hotline = new HotlineEntities())
            {
                var data = connect.GetTicketsData(hotline, ISModelStr);
                tckComplete = data
                    .Where(q => externalTickets.Contains(q.Id))
                    .ToList();
                // .Where(q=> q.Datum >= DateTime.Now.AddDays(-180) || q.Stav.ToLower() != "archiv")
                // .ToList();
                foreach (var x in tckComplete) {
                   x.Kalkulace =  connect.GetKalkulace(x);
                 }
                   
            }
            MOSD_External.ExtendingTicketsWithExternal(ref tckComplete);
            
            return tckComplete;
        }
        public Stream GetListOfSupplierDueTicketsToExcel(IEnumerable<Ticket> tickets, string author)
        {

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var ms = new MemoryStream();
            using (var p = new ExcelPackage(ms))
            {
                var sheet = p.Workbook.Worksheets.Add("Seznam");

                var rowIndex = 1;
                var col = 1;
                sheet.Cells[rowIndex, col++].Value = "HTL";
                sheet.Cells[rowIndex, col++].Value = "Typ záznamu";
                sheet.Cells[rowIndex, col++].Value = "IS";
                sheet.Cells[rowIndex, col++].Value = "Subsystém";
                sheet.Cells[rowIndex, col++].Value = "Modul";
                sheet.Cells[rowIndex, col++].Value = "Popis";
                sheet.Cells[rowIndex, col++].Value = "Založeno";
                sheet.Cells[rowIndex, col++].Value = "Poslední aktivita";
                sheet.Cells[rowIndex, col++].Value = "Zpracoval";
                sheet.Cells[rowIndex, col++].Value = "Stav";
                sheet.Cells[rowIndex, col++].Value = "Tým";
                sheet.Cells[rowIndex, col++].Value = "Dodavatel";
                sheet.Cells[rowIndex, col++].Value = "ID dodavatele";

                sheet.Cells[rowIndex, col++].Value = "SLA kategorie (MO)";
                sheet.Cells[rowIndex, col++].Value = "SLA název (dodavatel)";
                sheet.Cells[rowIndex, col++].Value = "SLA čas na řešení";

           //     sheet.Cells[rowIndex, col++].Value = "Celková doba strávená u dodavatele (hod)";
                sheet.Cells[rowIndex, col++].Value = "Změna termínu PM";

                sheet.Cells[rowIndex, col++].Value = "Události";
                /*
                sheet.Cells[rowIndex, col++].Value = "Termín týmu";
                sheet.Cells[rowIndex, col++].Value = "Termín SLA";
                sheet.Cells[rowIndex, col++].Value = "Dní po termínu týmu/SLA";
                sheet.Cells[rowIndex, col++].Value = "Typ prodlení";
                sheet.Cells[rowIndex, col++].Value = "Kategorie";
                */
                rowIndex++;
                foreach (var row in tickets)
                {
                    col = 1;
                    sheet.Cells[rowIndex, col++].Value = row.Id;
                    sheet.Cells[rowIndex, col++].Value = row.TypZaznamu;
                    sheet.Cells[rowIndex, col++].Value = row.InfSys;
                    sheet.Cells[rowIndex, col++].Value = row.Subsystem;
                    sheet.Cells[rowIndex, col++].Value = row.Modul;
                    sheet.Cells[rowIndex, col++].Value = row.Strucne;
                    if (row.Datum != null)
                    {
                        sheet.Cells[rowIndex, col].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.Datum);
                        sheet.Cells[rowIndex, col++].Style.Numberformat.Format = "mm-dd-yy";
                    }
                    if (row.LastActivity != null)
                    {
                        sheet.Cells[rowIndex, col].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.LastActivity);
                        sheet.Cells[rowIndex, col++].Style.Numberformat.Format = "mm-dd-yy";
                    }
                    sheet.Cells[rowIndex, col++].Value = row.Zpracoval;
                    sheet.Cells[rowIndex, col++].Value = row.Stav;
                    sheet.Cells[rowIndex, col++].Value = row.ResTym;
                    sheet.Cells[rowIndex, col++].Value = row.Dodavatel;
                    sheet.Cells[rowIndex, col++].Value = row.TicketExternal.SupplierTag;

                    sheet.Cells[rowIndex, col++].Value = row.Dulezitost;
                    sheet.Cells[rowIndex, col++].Value = row.TicketExternal.SLAName;
                    sheet.Cells[rowIndex, col++].Value = row.TicketExternal.SLATime;
                 //   sheet.Cells[rowIndex, col++].Value = row.TicketExternal.HoursAtSupplier;
                    sheet.Cells[rowIndex, col++].Value = (row.TicketExternal.AllowedTimeProlong)? "Prodlouženo" : "";

                    string transfers = string.Join(Environment.NewLine, row.TicketExternal.Workflow.OrderBy(q => q.Date).Select(q => q.Date.ToString("dd.MM.yyyy HH:mm:ss") + " - " + GetTransferTypeForUser(q.TransferType) + " " + q.Supplier));
                    if (!string.IsNullOrEmpty(transfers)) {
                        sheet.Cells[rowIndex, col].AddComment(transfers, "SD");
                        sheet.Cells[rowIndex, col].Comment.AutoFit = true;
                        sheet.Cells[rowIndex, col].Value = "V komentáři"; 
                    }
                    sheet.Cells[rowIndex, col++].Style.Font.Italic = true;

                    /*
                    if (row.DatResTym != null)
                    {
                        sheet.Cells[rowIndex, col].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.DatResTym);
                        sheet.Cells[rowIndex, col++].Style.Numberformat.Format = "mm-dd-yy";
                    }

                    if (row.SLADeadline != null && row.Stav == "dodavatel")
                    {
                        sheet.Cells[rowIndex, col].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.SLADeadline);
                        sheet.Cells[rowIndex, col++].Style.Numberformat.Format = "mm-dd-yy";
                    }

                    if (row.SLADeadline != null && row.Stav == "dodavatel")
                    {
                        double days = (Math.Floor(MOSD_External.CalculateWorkdays((DateTime)row.SLADeadline, DateTime.Now).TotalDays));
                        sheet.Cells[rowIndex, col++].Value = (days > 0) ? Math.Abs(days).ToString() : "";
                        sheet.Cells[rowIndex, col++].Value = (days > 0) ? "SLA" : "";
                    }
                    else if (row.DatResTym != null)
                    {
                        double days = Math.Floor((DateTime.Now - (DateTime)row.DatResTym).TotalDays);
                        sheet.Cells[rowIndex, col++].Value = (days > 0) ? Math.Abs(days).ToString() : "";
                        sheet.Cells[rowIndex, col++].Value = (days > 0) ? "TT" : "";
                    }
                    sheet.Cells[rowIndex, col++].Value = row.Dulezitost;
                    */
                    rowIndex++;
                }
                sheet.Cells.AutoFitColumns();

                p.Workbook.Properties.Title = "Sestava záznamů přenesených k dodavateli";
                p.Workbook.Properties.LastModifiedBy = user;
                p.Workbook.Properties.Author = "ServiceDesk FIS - " + user;
                p.Workbook.Properties.Application = "ServiceDesk FIS";
                p.Workbook.Properties.Company = "SE MO a AF";
                p.Save();
            }
            ms.Position = 0;
            return ms;
        }
        private string GetTransferTypeForUser(int? id)
        {
            switch (id)
            {
                case 1:
                    return "Založení tiketu u dodavatele";
                case 2:
                    return "Předání tiketu dodavateli";
                case 3:
                    return "Převzetí tiketu od dodavatele";
                case 5:
                    return "Archivováno u dodavatele";
                case 10:
                    return "Žádost dodavatele o prodloužení termínu";
                case 11:
                    return "Prodloužení/změna termínu PM MO";
                default:
                    return "Neznámý typ transferu";

            }
        }
        public Stream GetListOfOverallSupplierDueTicketsToExcel(IEnumerable<Ticket> tickets, string author)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var ms = new MemoryStream();
            using (var p = new ExcelPackage(ms))
            {
                var sheet = p.Workbook.Worksheets.Add("Seznam");

                var rowIndex = 1;
                var col = 1;
                sheet.Cells[rowIndex, col++].Value = "HTL";
                sheet.Cells[rowIndex, col++].Value = "Typ záznamu";
                sheet.Cells[rowIndex, col++].Value = "IS";
                sheet.Cells[rowIndex, col++].Value = "Subsystém";
                sheet.Cells[rowIndex, col++].Value = "Modul";
                sheet.Cells[rowIndex, col++].Value = "Popis";
                sheet.Cells[rowIndex, col++].Value = "Založeno";
                sheet.Cells[rowIndex, col++].Value = "Poslední aktivita";
                sheet.Cells[rowIndex, col++].Value = "Zpracoval";
                sheet.Cells[rowIndex, col++].Value = "Stav";
                sheet.Cells[rowIndex, col++].Value = "Akc.kalkulace bez DPH";

                sheet.Cells[rowIndex, col++].Value = "ID dodavatele";

                sheet.Cells[rowIndex, col++].Value = "SLA kategorie (MO)";
                sheet.Cells[rowIndex, col++].Value = "SLA název (dodavatel)";
                sheet.Cells[rowIndex, col++].Value = "SLA čas na řešení";

                sheet.Cells[rowIndex, col++].Value = "Změna termínu PM";

                sheet.Cells[rowIndex, col++].Value = "Události";
                sheet.Cells[rowIndex, col++].Value = "Dodavatel";
                sheet.Cells[rowIndex, col++].Value = "Dní penále";
                sheet.Cells[rowIndex, col++].Value = "Odůvodnění";
                sheet.Cells[rowIndex, col++].Value = "Penalizované dny";
                sheet.Cells[rowIndex, col++].Value = "Chyba výpočtu";

                rowIndex++;
                foreach (var row in tickets)
                {

                    var penalties = new PenaltyCalculation.TicketPenaltyCalculator().CalculatePenalty(row);
                    foreach (var penalty in penalties) {
                        col = 1;
                        sheet.Cells[rowIndex, col++].Value = row.Id;
                        sheet.Cells[rowIndex, col++].Value = row.TypZaznamu;
                        sheet.Cells[rowIndex, col++].Value = row.InfSys;
                        sheet.Cells[rowIndex, col++].Value = row.Subsystem;
                        sheet.Cells[rowIndex, col++].Value = row.Modul;
                        sheet.Cells[rowIndex, col++].Value = row.Strucne;
                        if (row.Datum != null)
                        {
                            sheet.Cells[rowIndex, col].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.Datum);
                            sheet.Cells[rowIndex, col++].Style.Numberformat.Format = "mm-dd-yy";
                        }
                        if (row.LastActivity != null)
                        {
                            sheet.Cells[rowIndex, col].Value = Helper.GetExcelDecimalValueForDate((DateTime)row.LastActivity);
                            sheet.Cells[rowIndex, col++].Style.Numberformat.Format = "mm-dd-yy";
                        }
                        sheet.Cells[rowIndex, col++].Value = row.Zpracoval;
                        sheet.Cells[rowIndex, col++].Value = row.Stav;

                        sheet.Cells[rowIndex, col].Style.Numberformat.Format = "#,##0.00 \"Kč\"";
                        sheet.Cells[rowIndex, col++].Value = row.Kalkulace?
                                                                    .Where(q => (q.Akceptace == "Akceptováno" || q.Akceptace == "Fakturováno" || q.Akceptace == "Fakturovat"))
                                                                    .OrderByDescending(q => q.DatumAkceptace)
                                                                    .FirstOrDefault()?
                                                                    .Cena;



                        sheet.Cells[rowIndex, col++].Value = row.TicketExternal.SupplierTag;

                        sheet.Cells[rowIndex, col++].Value = row.Dulezitost;
                        sheet.Cells[rowIndex, col++].Value = row.TicketExternal.SLAName;
                        sheet.Cells[rowIndex, col++].Value = row.TicketExternal.SLATime;

                        sheet.Cells[rowIndex, col++].Value = (row.TicketExternal.AllowedTimeProlong) ? "Prodlouženo" : "";

                        string transfers = string.Join(Environment.NewLine, row.TicketExternal.Workflow.OrderBy(q => q.Date).Select(q => q.Date.ToString("dd.MM.yyyy HH:mm:ss") + " - " + GetTransferTypeForUser(q.TransferType) + " " + q.Supplier + " " + GetDeadlineForTransfer(q.TransferType, q.DTParam, row.TypZaznamu)));
                        if (!string.IsNullOrEmpty(transfers))
                        {
                            sheet.Cells[rowIndex, col].AddComment(transfers, "SD");
                            sheet.Cells[rowIndex, col].Comment.AutoFit = true;
                            sheet.Cells[rowIndex, col].Value = "V komentáři";
                        }
                        sheet.Cells[rowIndex, col++].Style.Font.Italic = true;
                        if (penalties.Count()>1)
                        {
                            sheet.Cells[rowIndex, col].AddComment("Pro každého dodavatele tiketu " + row.Id + " je vypočítána penalizace samostatně.", "SD");
                        }
                        sheet.Cells[rowIndex, col++].Value = penalty.Supplier + ((penalties.Count()>1)? "*" : "");

                        sheet.Cells[rowIndex, col++].Value = penalty.PenaltyDays;
                        if (penalty.Reason.Count() > 0)
                        {
                            sheet.Cells[rowIndex, col].AddComment(string.Join(Environment.NewLine, penalty.Reason), "SD");
                            sheet.Cells[rowIndex, col].Comment.AutoFit = true;
                            sheet.Cells[rowIndex, col].Value = "V komentáři";
                        }
                        sheet.Cells[rowIndex, col++].Style.Font.Italic = true;

                        if (penalty.PenaltyDays > 0)
                        {
                            sheet.Cells[rowIndex, col].AddComment(string.Join(Environment.NewLine, penalty.PenalizedDays.Select(q => q.ToString("dd.MM.yyyy"))), "SD");
                            sheet.Cells[rowIndex, col].Comment.AutoFit = true;
                            sheet.Cells[rowIndex, col].Value = "V komentáři";
                        }
                        sheet.Cells[rowIndex, col++].Style.Font.Italic = true;
                        sheet.Cells[rowIndex, col++].Value = penalty.ErrorMessage;

                        sheet.Cells[rowIndex, col].Hyperlink = new Uri("https://hotline.fis.acr/zobraz_zaznam.asp?pid=" + row.PID);
                        sheet.Cells[rowIndex, col++].Value = "Link Hotline";
                        sheet.Cells[rowIndex, col].Hyperlink = new Uri("https://servicedesk.fis.acr/Hotline/Ticket/Details/" + row.Id);
                        sheet.Cells[rowIndex, col++].Value = "Link ServiceDesk";
                        rowIndex++;
                    }
                }
                sheet.Cells.AutoFitColumns();

                p.Workbook.Properties.Title = "Sestava záznamů přenesených k dodavateli";
                p.Workbook.Properties.LastModifiedBy = user;
                p.Workbook.Properties.Author = "ServiceDesk FIS - " + user;
                p.Workbook.Properties.Application = "ServiceDesk FIS";
                p.Workbook.Properties.Company = "SE MO a AF";
                p.Save();
            }
            ms.Position = 0;
            return ms;

        }

        private string GetDeadlineForTransfer(int? transferType, DateTime? dTParam, string typZaznamu)
        {
            if (dTParam != null)
            {
                if ((transferType == 1 && typZaznamu != "NES") || (transferType == 2 && typZaznamu != "NES") || transferType == 11)
                {
                    return "s termínem plnění " + ((DateTime)dTParam).ToString("dd.MM.yyyy");
                }
            }
            return "";
        }

        public IEnumerable<Ticket> GetTicketsWithWorkflowStart(IEnumerable<Ticket> tickets)
        {
           return tickets.Where(q => q.TicketExternal.Workflow.Any(z => z.TransferType == 1));
        }

    }
    public static class PenaltyCalculation
    {
        public static class TransferTypes
        {
            public const int U1_ZalozeniTiketuUDodavatele = 1;
            public const int U2_PredaniTiketuDodavateli = 2;
            public const int U3_PrevzetiTiketuOdDodavatele = 3;
            public const int U5_ArchivaceTiketuUDodavatele = 5;
            public const int U10_PozadavekNaZmenuTerminuDodavatelem = 10;
            public const int U11_SchvaleniZmenyTerminu = 11;
        }

        public class PenaltyCalculationResult
        {
            public string Supplier { get; set; }
            public int PenaltyDays => PenalizedDays.Count();
            public List<string> Reason { get; set; } = new List<string>();
            public List<DateTime> PenalizedDays { get; set; } = new List<DateTime>();
            public string ErrorMessage { get; set; } = null;
            public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        }

        public class TicketPenaltyCalculator
        {
            private List<DateTime> _holidays = null;


            public List<PenaltyCalculationResult> CalculatePenalty(Ticket ticket)
            {

                List<PenaltyCalculationResult> pcr = new List<PenaltyCalculationResult>();

                // státní svátky
                _holidays = DataLayer.Hotline.MOSD_External.GetCzechHolidaysForYears(ticket.Datum.Value.Year, DateTime.Now.Year);


                // seřazené wfl podle času události
                var sortedWorkflow = ticket.TicketExternal.Workflow.OrderBy(e => e.Date).ToList();
                if (sortedWorkflow.Count() == 0)
                {
                    pcr.Add(new PenaltyCalculationResult() { ErrorMessage = "Na tiketu nejsou žádné workflow" });
                }
                else
                {
                    if (sortedWorkflow.Any(q => (q.TransferType == TransferTypes.U1_ZalozeniTiketuUDodavatele)))
                    {
                        var suppliers = sortedWorkflow.Select(q => q.Supplier).Where(q => !string.IsNullOrEmpty(q)).Distinct();
                        if (suppliers.Count() > 1)
                        {
                            foreach(string supplier in suppliers)
                            {
                                PrepareCalculationResult(ticket, PrepareSortedWorkflowForMultipleSuppliers(sortedWorkflow, supplier), ref pcr, supplier);
                            }
                        }
                        else
                        {
                            PrepareCalculationResult(ticket, sortedWorkflow, ref pcr);
                        }
                    }
                    else
                    {
                        pcr.Add(new PenaltyCalculationResult() { ErrorMessage = "Tiket nemá inciační událost" });
                    }
                }
                return pcr;
            }

            private List<TicketExternalWorkflow> PrepareSortedWorkflowForMultipleSuppliers(List<TicketExternalWorkflow> sortedWorkflow, string supplier)
            {
                 List<TicketExternalWorkflow> oneSupplierWorkflow = new List<TicketExternalWorkflow>();
                bool enableCatchingSupplier = false;
                foreach (TicketExternalWorkflow workflow in sortedWorkflow)
                {
                    if (!string.IsNullOrEmpty(workflow.Supplier))
                    {
                        if(workflow.Supplier == supplier)
                        {
                            enableCatchingSupplier = true;
                        } else
                        {
                            enableCatchingSupplier = false;
                        }

                    }
                    if (enableCatchingSupplier)
                    {
                        oneSupplierWorkflow.Add(workflow);
                    }
                }
                return oneSupplierWorkflow;
            }

            private void PrepareCalculationResult(Ticket ticket, List<TicketExternalWorkflow> sortedWorkflow, ref List<PenaltyCalculationResult> pcr, string differentSupplier = null)
            {
                string currentSupplier = (differentSupplier==null)?ticket.Dodavatel : differentSupplier;


                if (ticket.TypZaznamu == "NES")
                {
                    // hodin na SLA
                    int initialSlaHoursForTicket = Convert.ToInt32(DataLayer.Hotline.TicketExternal_SlaCache.StaticCache.RetriveSlaHours(ticket.Dulezitost));
                    int secondsSlaLeft = initialSlaHoursForTicket * 60 * 60;
                    bool timeDriven = true; // zda je tiket řízen časem nebo započtaými dny prodlení dny
                    List<string> reason = new List<string>(); // důvod penalizace
                    List<DateTime> penalizedDay = new List<DateTime>(); // dny prodlení k penalizaci

                    if (differentSupplier != null)
                    {
                        reason.Add("T00 | Tiket byl transferován mezi dodavateli.");
                    }

                    for (int i = 0; i < sortedWorkflow.Count; i++)
                    {
                        var currentEvent = sortedWorkflow[i];

                        if (currentEvent.TransferType == TransferTypes.U1_ZalozeniTiketuUDodavatele || currentEvent.TransferType == TransferTypes.U2_PredaniTiketuDodavateli)
                        {
                            DateTime segmentStart = currentEvent.Date;

                            // Dohledat datum vrácení od dodavatele
                            DateTime? segmentEnd = GetWorkflowTransferReturnDate(i, sortedWorkflow);


                            if (timeDriven)
                            { // pokud se tiket řídí podle času SLA
                              // spočítat počet vteřin mezi odesláním a vrácením
                                int secondsBetweenDates = GetWorkingSecondsBetweenDates(segmentStart, segmentEnd, true);

                                if (secondsSlaLeft - secondsBetweenDates < 0)
                                { // termín prošvihnut

                                    // řízení podle dní
                                    timeDriven = false;

                                    // Datum, do kterého se počítá deadline na časové sla
                                    DateTime deadline = GetDeadline(segmentStart, secondsSlaLeft, true);

                                    // dopočítat počet dní v prodlení s možnou variantou prodloužení
                                    DateTime? prolongationFrom, prolongationTo = null;


                                    // vypočítat možné prodloužení
                                    GetProlongationSegment(i, sortedWorkflow, out prolongationFrom, out prolongationTo, true);


                                    // vypočítá penalizaci a doplní počet dní k sankcionování
                                    GetPenaltyDays(deadline, segmentStart, segmentEnd, prolongationFrom, prolongationTo, ref reason, ref penalizedDay, true);

                                }
                                else
                                { // zbývá ještě čas na vyřešení
                                    reason.Add("T01 | Obrátka (" + segmentStart + " - " + segmentEnd + ") proběhla v čase vyhrazeném na SLA, počáteční čas: " + Math.Round((decimal)(secondsSlaLeft / 60 / 60), 2) + "h, odčítaný čas: " + Math.Round((decimal)(secondsBetweenDates / 60 / 60), 2) + "h");
                                    secondsSlaLeft = secondsSlaLeft - secondsBetweenDates;
                                }
                            }
                            else
                            { // pokud už tiket není řízený časem

                                // dopočítat počet dní v prodlení s možnou variantou prodloužení
                                DateTime? prolongationFrom, prolongationTo = null;

                                // vypočítat možné prodloužení, které mohlo nastat
                                GetProlongationSegment(i, sortedWorkflow, out prolongationFrom, out prolongationTo, true);

                                // vypočítá případnou penalizaci a doplní počet dní k sankcionování (deadline je null, protože dtparam se nikdy nepředává, takže se počítá z případné prolngace)
                                GetPenaltyDays(null, segmentStart, segmentEnd, prolongationFrom, prolongationTo, ref reason, ref penalizedDay, true);

                            }
                        }
                    }
                    CheckProlongationPardon(sortedWorkflow, ref reason, ref penalizedDay);
                    pcr.Add(new PenaltyCalculationResult()
                    {
                        PenalizedDays = penalizedDay,
                        Reason = reason,
                        Supplier = currentSupplier
                    });
                }

                else if (ticket.TypZaznamu == "PMP")
                {
                    List<string> reason = new List<string>(); // důvod penalizace
                    List<DateTime> penalizedDay = new List<DateTime>(); // dny prodlení k penalizaci

                    if (differentSupplier != null)
                    {
                        reason.Add("T00 | Tiket byl transferován mezi dodavateli.");
                    }
                    for (int i = 0; i < sortedWorkflow.Count; i++)
                    {
                        var currentEvent = sortedWorkflow[i];

                        if (currentEvent.TransferType == TransferTypes.U1_ZalozeniTiketuUDodavatele || currentEvent.TransferType == TransferTypes.U2_PredaniTiketuDodavateli)
                        {
                            DateTime segmentStart = currentEvent.Date;

                            // Dohledat datum vrácení od dodavatele
                            DateTime? segmentEnd = GetWorkflowTransferReturnDate(i, sortedWorkflow);

                            // dopočítat počet dní v prodlení s možnou variantou prodloužení
                            DateTime? prolongationFrom, prolongationTo = null;

                            // vypočítat možné prodloužení, které mohlo nastat
                            GetProlongationSegment(i, sortedWorkflow, out prolongationFrom, out prolongationTo, false);

                            // vypočítá případnou penalizaci a doplní počet dní k sankcionování (deadline je null, protože dtparam se nikdy nepředává, takže se počítá z případné prolngace)
                            GetPenaltyDays(currentEvent.DTParam, segmentStart, segmentEnd, prolongationFrom, prolongationTo, ref reason, ref penalizedDay, false);
                        }
                    }
                    CheckProlongationPardon(sortedWorkflow, ref reason, ref penalizedDay);
                    pcr.Add(new PenaltyCalculationResult()
                    {
                        PenalizedDays = penalizedDay,
                        Reason = reason,
                        Supplier = currentSupplier
                    });
                }

                else if (ticket.TypZaznamu == "PNF")
                {
                    List<string> reason = new List<string>(); // důvod penalizace
                    List<DateTime> penalizedDay = new List<DateTime>(); // dny prodlení k penalizaci

                    if (differentSupplier != null)
                    {
                        reason.Add("T00 | Tiket byl transferován mezi dodavateli.");
                    }
                    for (int i = 0; i < sortedWorkflow.Count; i++)
                    {
                        var currentEvent = sortedWorkflow[i];

                        if (currentEvent.TransferType == TransferTypes.U1_ZalozeniTiketuUDodavatele || currentEvent.TransferType == TransferTypes.U2_PredaniTiketuDodavateli)
                        {
                            DateTime segmentStart = currentEvent.Date;

                            // Dohledat datum vrácení od dodavatele
                            DateTime? segmentEnd = GetWorkflowTransferReturnDate(i, sortedWorkflow);

                            // dopočítat počet dní v prodlení s možnou variantou prodloužení
                            DateTime? prolongationFrom, prolongationTo = null;

                            // vypočítat možné prodloužení, které mohlo nastat
                            GetProlongationSegment(i, sortedWorkflow, out prolongationFrom, out prolongationTo, false);

                            // vypočítá případnou penalizaci a doplní počet dní k sankcionování (deadline je null, protože dtparam se nikdy nepředává, takže se počítá z případné prolngace)
                            GetPenaltyDays(currentEvent.DTParam, segmentStart, segmentEnd, prolongationFrom, prolongationTo, ref reason, ref penalizedDay, true);
                        }
                    }
                    CheckProlongationPardon(sortedWorkflow, ref reason, ref penalizedDay);

                    pcr.Add(new PenaltyCalculationResult()
                    {
                        PenalizedDays = penalizedDay,
                        Reason = reason,
                        Supplier = currentSupplier
                    });
                }
                else
                {
                    pcr.Add(new PenaltyCalculationResult() { ErrorMessage = "Neznámý typ záznamu " + ticket.TypZaznamu });
                }
            }

            private void CheckProlongationPardon(List<TicketExternalWorkflow> sortedWorkflow, ref List<string> reason, ref List<DateTime> penalizedDay)
            {
                var prolongs = sortedWorkflow.Where(q => q.TransferType == 11);
                if (prolongs.Count() > 0)
                {
                    var prolongedTo = prolongs.Max(q => q.DTParam);
                    foreach(var d in penalizedDay.OrderByDescending(q=>q.Date))
                    {
                        if (d <= prolongedTo) penalizedDay.Remove(d);
                    }

                    reason.Add("T14 | Byl prodloužen termín do " + prolongedTo + ". Dny napočítané do tohoto dne včetně jsou nulovány.");

                }
            }

            /// <summary>
            /// Vypočítá počet dní k penalizaci
            /// </summary>
            /// <param name="deadline">Do kdy měl dodavatel vrátit tiket</param>
            /// <param name="segmentEnd">Kdy jej skutečně vrátil</param>
            /// <param name="prolongationFrom">Od kdy bylo povoleno prodloužení</param>
            /// <param name="prolongationTo">Do kdy bylo povoleno prodloužení</param>
            /// <param name="datesPenalized">Penalizované dny</param>
            /// <param name="reason">seznam důvodů pro penalizaci</param>
            private void GetPenaltyDays(DateTime? deadline, DateTime? segmentStart, DateTime? segmentEnd, DateTime? prolongationFrom, DateTime? prolongationTo, ref List<string> reason, ref List<DateTime> datesPenalized, bool onlyWorkDays)
            {
             /*   if (resetDates)
                {
                    //   if (prolongationTo != null)
                    //   {
                    datesPenalized.RemoveRange(0, datesPenalized.Count());
                    reason.Add("T14 | Byl prodloužen termín do " + prolongationTo + ". Dny napočítané do této události jsou nulovány.");

                    //   }

                }*/

                if (segmentEnd == null)
                { // tiket je aktuálně u dodavatele
                    segmentEnd = DateTime.Now;
                    reason.Add("T02 | Obrátka nedoběhla, tiket se stále nachází u dodavatele, odesláno dodavateli: " + segmentStart + ", pro výpočty stanoven fiktivní čas návratu pro výpočet penalizace na: " + segmentEnd);
                }
                if (deadline == null)
                {
                 //   deadline = prolongationTo;
                 //   calculations.Add("Deadline pro tuto obrátku nebyl určen, odesláno dodavateli: " + segmentStart + ", vráceno od dodavatele: " + segmentStart + ", pro výpočty je deadline určen čas případné prolongace: " + deadline);
                }

                if (deadline >= segmentEnd)
                { // Dodavatel stihl dodat včas
                    reason.Add("T03 | Obrátka (" + segmentStart + " - " + segmentEnd + ") proběhla včas, deadline: " + deadline + ", odesláno dodavateli: " + segmentStart + ", vráceno od dodavatele: " + segmentEnd);
                }
                else
                { 
                    // Dodavatel nestihl dodat včas
                    if (prolongationTo == null || prolongationTo < deadline)
                    {
                        // nedošlo k prodloužení, tudíž se započítává celá doba mezi segment end a deadline
                        if (deadline == null)
                        { // pokud je deadline null, počítá se čas od data předání dodavateli
                            int penalty = GetPenaltyDaysBetweenTwoDates(segmentStart, segmentEnd, ref datesPenalized, onlyWorkDays);
                            reason.Add("T04 | Obrátka (" + segmentStart + " - " + segmentEnd + ") NEproběhla včas, započítáno prodlení od: " + PrepareDisplayInfoMidnight(segmentStart) + " do: " + segmentEnd + ", započítáno dní: " + penalty);
                        } else
                        {
                            int penalty = GetPenaltyDaysBetweenTwoDates(deadline, segmentEnd, ref datesPenalized, onlyWorkDays);
                            reason.Add("T05 | Obrátka (" + segmentStart + " - " + segmentEnd + ") NEproběhla včas, započítáno prodlení od: " + PrepareDisplayInfoMidnight(deadline) + " do: " + segmentEnd + ", započítáno dní: " + penalty);
                        }
                    } else if(prolongationTo != null && prolongationFrom == null)
                    {
                        // došlo k prodloužení bez předchozího požádání o prodloužen termínu
                        // vypočítat, kdy nastal deadline nebo předání tiketu k dodavateli
                        if(prolongationTo > segmentEnd)
                        {
                            // dodavatel stihl vrátit před prodloužením termínu
                            reason.Add("T06 | Tato obrátka proběhla včas do prodlouženého termínu - odesláno dodavateli: " + segmentStart + ", vráceno od dodavatele: " + segmentEnd + ", termín prodloužení: " + prolongationTo);

                        } else
                        {
                            // dodavatel nestihl vrátit včas
                            DateTime? from = (prolongationTo < segmentStart) ? segmentStart : prolongationTo;
                            int penalty = GetPenaltyDaysBetweenTwoDates(from, segmentEnd, ref datesPenalized, onlyWorkDays);
                            reason.Add("T07 | Obrátka (" + segmentStart + " - " + segmentEnd + ") NEproběhla včas, započítáno prodlení od: " + from + " do: " + segmentEnd + ", započítáno dní: " + penalty);
                        }
                    } else if(prolongationTo != null && prolongationFrom != null)
                    {
                        // došlo k prodloužení termínu po předchozím požadavku na prodloužení termínu
                        // dodavatel ale nestihl prodloužit před deadlinem
                        if(deadline == null)
                        {
                            deadline = segmentStart;
                            reason.Add("T08 | Deadline pro výpočet doby prodloužení v obrátce " + segmentStart + " - " + segmentEnd + " nastaven na " + deadline + ", termín prodloužení od: " + prolongationFrom + ", termín prodloužení do: " + prolongationTo);

                        }

                        // #######
                        // Vynechat jen tento blok, aby se nepočítalo opomenutí prodloužení dodavatelem
                        // #######

                        /*
                        // začátek obrátky
                        if (deadline > prolongationFrom)
                        {
                            // dodavatel stihl vrátit před prodloužením termínu
                            reason.Add("T09 | Začátek obrátky  (" + segmentStart + " - " + segmentEnd + ") byl včas prodloužen, termín prodloužení: " + PrepareDisplayInfoMidnight(prolongationTo) + ", deadline: " + deadline);
                        }
                        else
                        {
                            // dodavatel nestihl požádat o prodloužení včas
                            int penalty = GetPenaltyDaysBetweenTwoDates(deadline, prolongationFrom, ref datesPenalized, onlyWorkDays);
                            reason.Add("T10 | Obrátka (" + segmentStart + " - " + segmentEnd + ") NEproběhla včas, dodavatel nestihl požádat včas o prodloužení  termínu - započítáno prodlení od : " + PrepareDisplayInfoMidnight(deadline) + " do: " + prolongationFrom + ", započítáno dní: " + penalty);
                        }
                        */

                        // konec obrátky
                        if (prolongationTo > segmentEnd)
                        {
                            // dodavatel stihl vrátit před prodloužením termínu
                            reason.Add("T11 | Konec obrátky  (" + segmentStart + " - " + segmentEnd + ") proběhl včas do prodlouženého termínu, termín prodloužení: " + prolongationTo);
                        }
                        else
                        {
                            // dodavatel nestihl vrátit včas
                            int penalty = GetPenaltyDaysBetweenTwoDates(prolongationTo, segmentEnd, ref datesPenalized, onlyWorkDays);
                            reason.Add("T12 | Obrátka (" + segmentStart + " - " + segmentEnd + ") NEproběhla včas, započítáno prodlení od: " + PrepareDisplayInfoMidnight(prolongationTo) + " do: " + segmentEnd + ", započítáno dní: " + penalty);
                        }
                    } else
                    {
                        reason.Add("T13 | !!! Neznámá kombinace parametrů pro výpočet prodlení.");
                    }
                }
            }

            private string PrepareDisplayInfoMidnight(DateTime? dt)
            {
                if (dt == null) return "";
                if (((DateTime)dt).Hour == 23 && ((DateTime)dt).Minute == 59 && ((DateTime)dt).Second == 59) { return dt + " (bez)"; }
                return dt.ToString();
            }

            /// <summary>
            /// Vypočítá počet pacovních dní mezi dvěma daty a vrátí počet dní
            /// Využívá funkci IsWorkingDay(DateTime) pro zjištění, zda se jedná o pracovní den
            /// Nezapočítává do prodlení 1. den z "from", pokud k prodlení nastalo během dne.
            /// Započítává poslední probíhající den z "to"
            /// </summary>
            /// <param name="from"></param>
            /// <param name="to"></param>
            /// <returns></returns>
            private int GetPenaltyDaysBetweenTwoDates(DateTime? from, DateTime? to, ref List<DateTime> penalizedBefore, bool onlyWorkDays)
            {
                if (from == null || to == null)
                {
                    // Dle zadání na obrázku vyhazujeme InvalidDataException
                    throw new InvalidDataException("Vstupní data 'from' a 'to' nesmí být null.");
                }

                // Převedeme nullable DateTime na DateTime, jelikož jsme ošetřili null hodnoty
                DateTime startDate = from.Value;
                DateTime endDate = to.Value;

                int penaltyDaysCount = 0;

                // Iteraci začneme ode dne *následujícího* po datu 'startDate.Date'.
                // Tím splníme pravidlo "Nezapočítává do prodlení 1. den z 'from'".
                // Pokud je endDate.Date ve stejný den jako startDate.Date (nebo dříve),
                // smyčka se neprovede a výsledek bude 0, což odpovídá situaci,
                // kdy "k prodlení nenastalo během dne" (tj. vyřešeno ve stejný den).
                DateTime currentDayToEvaluate = startDate.Date; // .AddDays(1); // neaplikovat toto pravidlo, ve smlouvě je "i započatý pracovní den"

                // aplikovat pravidlo, protože prodloužené dny jsou do půlnoci, tudíž je možné počítat až od dalšího dne
                if(startDate.Hour == 23 && startDate.Minute == 59 && startDate.Second == 59) { currentDayToEvaluate = currentDayToEvaluate.AddDays(1); }
                // Smyčka pokračuje až do (včetně) data 'endDate.Date'.
                // V kombinaci s kontrolou IsWorkingDay to splňuje pravidlo
                // "Započítává poslední probíhající den z 'to'".
                while (currentDayToEvaluate <= endDate.Date)
                {
                    // Nepenalizovat již jednou započítané pracovní dny
                    if (!penalizedBefore.Contains(currentDayToEvaluate))
                    {
                        // Využíváme metodu IsWorkingDay. Předpokládáme, že seznam svátků this._holidays
                        // je pro ni dostupný (např. předán jako parametr, jak je v tomto volání).
                        if (IsWorkingDay(currentDayToEvaluate) || !onlyWorkDays)
                        {
                            penaltyDaysCount++;
                            penalizedBefore.Add(currentDayToEvaluate);
                        }
                    }
                    currentDayToEvaluate = currentDayToEvaluate.AddDays(1);
                }

                return penaltyDaysCount;

            }


            /// <summary>
            /// 
            /// </summary>
            /// <param name="x"></param>
            /// <param name="sortedWorkflow"></param>
            /// <param name="prolongationFrom"></param>
            /// <param name="prolongationTo"></param>
            /// <param name="beforebound">hodnota true slouží pro případné hledání o prodloužení termínu do doby před převzetím k nám</param>
            private void GetProlongationSegment(int x, List<TicketExternalWorkflow> sortedWorkflow, out DateTime? prolongationFrom, out DateTime? prolongationTo, bool beforebound)
            {
                int? lastTakeBack = null;
                prolongationFrom = null;
                prolongationTo = null;

                for (int i = x + 1; i < sortedWorkflow.Count; i++)
                {
                    var currentEvent = sortedWorkflow[i];
                    if (currentEvent.TransferType == TransferTypes.U3_PrevzetiTiketuOdDodavatele) // hledáme kdy došlo k převzetí zpět, abychom si vymezeli rozsah hledání
                    {
                        lastTakeBack = i;
                        break;
                    }
                }
                if (lastTakeBack != null)
                {
                    for (int i = (int)lastTakeBack; i > ((beforebound)? 0 : x); i--)
                    {
                        var currentEvent = sortedWorkflow[i];
                        if (currentEvent.TransferType == TransferTypes.U11_SchvaleniZmenyTerminu) // hledáme, jestli byl schválený termín
                        {
                            prolongationTo = currentEvent.DTParam;
                            for (int z = i; z > x; z--)
                            {
                                var cEvent = sortedWorkflow[z];
                                if (cEvent.TransferType == TransferTypes.U10_PozadavekNaZmenuTerminuDodavatelem) // hledáme, jestli si požádali o prodloužení termínu, ale pouze v rozemezí od předání po vrácení
                                {
                                    prolongationFrom = cEvent.Date;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }

            }

            /// <summary>
            /// vrací datetime, kdy se přičítají vteřiny k výchozímu dny
            /// vteřiny se přičítají pouze v pracovní dny
            /// </summary>
            /// <param name="segmentStart"></param>
            /// <param name="secondsSlaLeft"></param>
            /// <returns></returns>
            private DateTime GetDeadline(DateTime segmentStart, int secondsSlaLeft, bool onlyWorkDays)
            {
                if (secondsSlaLeft <= 0)
                {
                    // Pokud není potřeba přičítat žádné sekundy (nebo je hodnota nepozitivní),
                    // vrátíme původní čas.
                    return segmentStart;
                }

                DateTime aktualniCilovyCas = segmentStart;
                // Pro výpočty se sekundami je vhodnější použít 'double',
                // protože TimeSpan.TotalSeconds vrací double.
                double zbyvajiciSekundyKDistribuci = secondsSlaLeft;

                while (zbyvajiciSekundyKDistribuci > 0)
                {
                    DateTime datumovaCastAktualnihoCile = aktualniCilovyCas.Date;

                    if (IsWorkingDay(datumovaCastAktualnihoCile) || !onlyWorkDays)
                    {
                        // Toto je pracovní den. Vypočítáme, kolik sekund SLA lze tento den spotřebovat.
                        // Konec aktuálního pracovního dne (00:00:00 následujícího dne).
                        DateTime konecAktualnihoPracovnihoDne = datumovaCastAktualnihoCile.AddDays(1);

                        // Počet sekund dostupných od 'aktualniCileovyCas' do konce tohoto pracovního dne.
                        double sekundyDostupneTentoDen = (konecAktualnihoPracovnihoDne - aktualniCilovyCas).TotalSeconds;

                        if (sekundyDostupneTentoDen >= zbyvajiciSekundyKDistribuci)
                        {
                            // Termín SLA končí v tento den. Přičteme zbývající sekundy.
                            aktualniCilovyCas = aktualniCilovyCas.AddSeconds(zbyvajiciSekundyKDistribuci);
                            zbyvajiciSekundyKDistribuci = 0; // Všechny sekundy byly rozděleny.
                        }
                        else
                        {
                            // SLA pokračuje i po tomto dni.
                            // Spotřebujeme všechny dostupné sekundy z tohoto dne a
                            // přesuneme 'aktualniCileovyCas' na začátek dalšího dne.
                            aktualniCilovyCas = konecAktualnihoPracovnihoDne;
                            zbyvajiciSekundyKDistribuci -= sekundyDostupneTentoDen;
                        }
                    }
                    else
                    {
                        // Není pracovní den. Přesuneme 'aktualniCilovyCas' na začátek dalšího dne (00:00:00).
                        // Časová složka 'aktualniCileovyCas' se pro tento nepracovní den efektivně přeskočí.
                        aktualniCilovyCas = datumovaCastAktualnihoCile.AddDays(1);
                    }
                }
                return aktualniCilovyCas;

            }

            private DateTime? GetWorkflowTransferReturnDate(int x, List<TicketExternalWorkflow> sortedWorkflow)
            {
                for (int i = x + 1; i < sortedWorkflow.Count; i++)
                {
                    var currentEvent = sortedWorkflow[i];
                    if (currentEvent.TransferType == TransferTypes.U3_PrevzetiTiketuOdDodavatele)
                    {
                        return currentEvent.Date;
                    }
                }
                return null; // v případě, že je tiket stále u dodavatele
            }

            private int GetWorkingSecondsBetweenDates(DateTime segmentStart, DateTime? segmentEnd, bool onlyWorkDays)
            {
                if (segmentEnd == null)
                { // není koncový termín, takže je tiket u dodavatele, nastavuje se tedy aktuální datum
                    segmentEnd = DateTime.Now;
                } 

                if (segmentEnd <= segmentStart)
                {
                    return 0; // Pokud je konec před nebo ve stejný čas jako začátek, vracíme 0 sekund.
                }

                double totalWorkingSeconds = 0;

                // Iterujeme přes každý den v zadaném časovém rozmezí.
                // Začínáme od data (bez času) segmentStart a končíme datem (bez času) segmentEnd.
                DateTime currentDateIterator = segmentStart.Date;

                while (currentDateIterator <= ((DateTime)segmentEnd).Date)
                {
                    // Zde voláme metodu IsWorkingDay. Předpokládáme, že seznam svátků '_holidays'
                    // je dostupný jako členská proměnná třídy (např. this._holidays).
                    // Pokud byste chtěli seznam svátků předávat jinak, bylo by nutné upravit volání
                    // nebo signaturu GetWorkingSecondsBetweenDates.
                    if (IsWorkingDay(currentDateIterator) || !onlyWorkDays) // Použití this._holidays
                    {
                        // Určení hranic aktuálního dne
                        DateTime currentDayActualStart = currentDateIterator; // 00:00:00 aktuálního dne
                        DateTime currentDayActualEnd = currentDateIterator.AddDays(1); // 00:00:00 následujícího dne

                        // Výpočet efektivního začátku a konce pro tento pracovní den
                        // s ohledem na původní segmentStart a segmentEnd.
                        // Efektivní začátek je pozdější z časů: začátek aktuálního dne NEBO segmentStart.
                        DateTime effectiveStartForDay = (segmentStart > currentDayActualStart) ? segmentStart : currentDayActualStart;

                        // Efektivní konec je dřívější z časů: konec aktuálního dne NEBO segmentEnd.
                        DateTime effectiveEndForDay = (((DateTime)segmentEnd) < currentDayActualEnd) ? ((DateTime)segmentEnd) : currentDayActualEnd;

                        // Přidáme sekundy pouze pokud je vypočítaný časový úsek platný (konec je po začátku).
                        if (effectiveEndForDay > effectiveStartForDay)
                        {
                            totalWorkingSeconds += (effectiveEndForDay - effectiveStartForDay).TotalSeconds;
                        }
                    }
                    currentDateIterator = currentDateIterator.AddDays(1);
                }

                // Metoda má vracet int, TotalSeconds je double. Přetypování na int oreže desetinnou část.
                return (int)totalWorkingSeconds;

            }

            private bool IsWorkingDay(DateTime date)
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    return false;
                return !_holidays.Any(h => h.Date == date.Date);
            }


        }
    }

    public static class ExtensionMethods
    {
        public static List<Model.Stats> GetStatsOpenedTickets(this List<Model.Stats> stats, IEnumerable<Model.Ticket> data)
        {
            string[] allowedExtendedRoles = new string[] { "PM", "VS" };
            stats.Add(new Stats() { Key = "TOTAL", Total = data.Count(), Description = string.Join(", ", data.SelectMany(q => q.Perm.Select(t => t.TymText)).Distinct()) });

            stats.Add(new Stats() { Key = "SUM_365", Name = "NES", Total = data.Where(q => q.TypZaznamu == "NES").Count(), Partial = data.Where(q => q.TypZaznamu == "NES" && q.Stav != "archiv").Count() });
            stats.Add(new Stats() { Key = "SUM_365", Name = "PMP", Total = data.Where(q => q.TypZaznamu == "PMP").Count(), Partial = data.Where(q => q.TypZaznamu == "PMP" && q.Stav != "archiv").Count() });
            stats.Add(new Stats() { Key = "SUM_365", Name = "PNF", Total = data.Where(q => q.TypZaznamu == "PNF").Count(), Partial = data.Where(q => q.TypZaznamu == "PNF" && q.Stav != "archiv").Count() });

            if (data.Any(q => q.Perm.Select(t => t.Key).Any(z => allowedExtendedRoles.Contains(z)))){ 
            stats.Add(new Stats() { Key = "SUM_365_DODAVATEL", Name = "NES", Total = data.Where(q => q.TypZaznamu == "NES" && (q.Stav == "dodavatel" || q.Stav == "pro dodavatele")).Count(), Partial = data.Where(q => q.TypZaznamu == "NES" && (q.Stav == "dodavatel" || q.Stav == "pro dodavatele") && q.DatResTym < DateTime.Now).Count() });
            stats.Add(new Stats() { Key = "SUM_365_DODAVATEL", Name = "PMP", Total = data.Where(q => q.TypZaznamu == "PMP" && (q.Stav == "dodavatel" || q.Stav == "pro dodavatele")).Count(), Partial = data.Where(q => q.TypZaznamu == "PMP" && (q.Stav == "dodavatel" || q.Stav == "pro dodavatele") && q.DatResTym < DateTime.Now).Count() });
            stats.Add(new Stats() { Key = "SUM_365_DODAVATEL", Name = "PNF", Total = data.Where(q => q.TypZaznamu == "PNF" && (q.Stav == "dodavatel" || q.Stav == "pro dodavatele")).Count(), Partial = data.Where(q => q.TypZaznamu == "PNF" && (q.Stav == "dodavatel" || q.Stav == "pro dodavatele") && q.DatResTym < DateTime.Now).Count() });


            stats.Add(new Stats() { Key = "SUM_365_DAY", Dictionary = data.Where(q => q.Stav != "archiv" && q.Datum > DateTime.Now.AddYears(-1).AddMonths(1)).OrderBy(q=>q.Datum).GroupBy(g => g.Datum.Value.Month).Select(q => new { Month = q.Key, Count = q.Count() }).ToList().ToDictionary(x => x.Month.ToString(), x => x.Count) });
            }
            data.Count();
            return stats;
        }
        public static List<Model.Stats> GetStatsDodavatelTickets(this List<Model.Stats> stats, IEnumerable<Model.Ticket> data)
        {
            return stats;
        }
        public static List<string> GetSubsystem<Ticket>(this IQueryable<Ticket> t)
        {
            var result = t.Select("Subsystem").Distinct().ToDynamicList<string>();
            return result; //.Cast<string>().ToList();
        }
        public static List<string> GetProcToVidim<Ticket>(this IEnumerable<Ticket> t)
        {
            var result1 = t.AsQueryable();
            var result2 = result1.Select("Perm");
            var result3 = result2.Distinct();
            var result = result3.ToDynamicList<string>();
            return result; //.Cast<string>().ToList();
        }
        public static List<string> GetModul<Ticket>(this IQueryable<Ticket> t)
        {
            var result = t.Select("Modul").Distinct().ToDynamicList<string>();
            return result; //.Cast<string>().ToList();
        }
        public static List<string> GetStav<Ticket>(this IQueryable<Ticket> t)
        {
            var result = t.Select("Stav").Distinct().ToDynamicList<string>();
            return result; //.Cast<string>().ToList();
        }
        public static IQueryable<Ticket> ApproveUserToTickets<Ticket>(this IQueryable<Ticket> t, string user, List<ISModel> ISModel, List<Tymy> TymModel, Permissions perm)
        {
            List<string> param = new List<string>();
            // načíst tickety, kde je uživatel jako zadavatel

            int i = 0;
            string wh = "";

            if (perm.Permission.Any(q => q.Name == "ZP"))
            {
                wh = wh.LinqAddOr("ZalozilLogin == @" + i);
                param.Add(user.ToLower());
                i++;
            }
            // kde je uživatel jako VS
            foreach (var p in perm.Permission.Where(q => q.Name == "VS"))
            {
                foreach (var val in p.Values)
                {
                    wh = wh.LinqAddOr("subsystem == @" + i);
                    param.Add(val);
                    i++;
                }
            }

            // kde je uživatel jako člen VFIS (FIS/ISSP) projektový manažer
            foreach (var p in perm.Permission.Where(q => q.Name == "PM"))
            {
                foreach (var val in p.Values)
                {
                    var ms = ISModel.Where(q => q.zkratka == val);
                    foreach (var m in ms)
                    {
                        foreach (var q in m.subsystems)
                        {
                            wh = wh.LinqAddOr("subsystem == @" + i);
                            param.Add(q.zkratka);
                            i++;
                        }
                    }
                }
            }

            // kde je uživatel jako člen ŘT
            foreach (var p in perm.Permission.Where(q => q.Name == "RT"))
            {
                foreach (var val in p.Values)
                {
                    var tm = TymModel.Where(q => q.nazev_z == val);
                    foreach (var ti in tm)
                    {
                        wh = wh.LinqAddOr("subsystem == @" + i);
                        param.Add(ti.subsystem);
                        i++;
                    }
                }
            }
            // kde uživatel má možnost vidět vše
            if (perm.Permission.Any(q => q.Name == "EO"))
            {
                wh = wh.LinqAddOr("1==1"); // "ZalozilLogin == @" + i);
            //    param.Add(user.ToLower());
            //    i++;
            }
            wh = "(" + wh + ")";
            t = t.Where(wh,param.ToArray());

            return t;
        }
    }
}
