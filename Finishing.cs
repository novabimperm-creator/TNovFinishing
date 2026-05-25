using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNovCommon;
using Newtonsoft.Json;

namespace TNovFinishing
{
    [Transaction(TransactionMode.Manual)]
    public class Finishing : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Отделка";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " ");
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            docName = docName.Replace(",", "");
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);

            #region Настройки логов
            // создание log - файла
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }
            #endregion

            //параметры
            Guid NFinishRoomParamGuid = new Guid("8b9d4aff-a6c8-4ad5-b0f5-442f2b87c765"); //N_Отделка.Помещение

            #region Сбор элементов

            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.WallType != null && w.WallType.Kind == WallKind.Basic)
                .ToList();

            var floors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .ToList();

            var ceilings = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Ceiling))
                .Cast<Ceiling>()
                .ToList();

            List<Element> elems = new List<Element>();
            foreach(var wall in walls)
            {
                Element type = doc.GetElement(wall.GetTypeId());
                if(type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Отделка")) elems.Add(doc.GetElement(wall.Id));
            }
            foreach (var floor in floors)
            {
                Element type = doc.GetElement(floor.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Пол")) elems.Add(doc.GetElement(floor.Id));
            }
            foreach (var ceiling in ceilings)
            {
                Element type = doc.GetElement(ceiling.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Потолок")) elems.Add(doc.GetElement(ceiling.Id));
            }
            if (elems.Count == 0) { Logger.Log("Отсутствуют элементы отделки. Завершение работы", 3); return Result.Cancelled; }

            int allcount = elems.Count;

            #endregion

            bool unhandledError = false;
            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try
                {
                    transaction.Start("TNov - Ведомость отделки");
                    Logger.Log("Открываем транзакцию", 1);

                    foreach (var elem in elems)
                    {
                        Logger.Log("Элемент " + elem.Id.IntegerValue.ToString(), 2);
                        Parameter roomParam = elem.get_Parameter(NFinishRoomParamGuid);
                        roomParam?.Set(""); //очищаем параметр, чтобы отработали апдейтеры
                    }


                    transaction.Commit();

                    Logger.Log("Закрываем транзакцию.", 1);
                }
                catch (Exception ex) {
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;
                    Logger.Log("Ошибка: " + ex.Message, 4);
                }
            }
            #endregion

            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }

            new InfoWindow280("Готово! Параметры отделки заполнены.").ShowDialog();

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
        

        
        
    }
}
