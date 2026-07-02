using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TNovCommon;
using Floor = Autodesk.Revit.DB.Floor;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace TNovFinishing
{
    
    [Transaction(TransactionMode.Manual)]
    public class Floors : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Генератор полов";
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
            string NFinishElemNaznParam = "Отделка.Помещение.Назначение";
            Guid NFinishElemGroupParamGuid = new Guid("60e4ba60-55ca-4922-8ce7-22a6c43c95c2"); //N_Отделка.ГруппаТекст
            BuiltInParameter roomNameParam = BuiltInParameter.ROOM_NAME;
            BuiltInParameter roomNaznParam = BuiltInParameter.ROOM_DEPARTMENT;
            Guid NFinishRoomGroupParamGuid = new Guid("76144285-f586-4eb7-af04-e4ad9902f67a"); //N_Отделка.Группа
            Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
            Guid TPolozhParamGuid = new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"); //Т_Положение
            Guid TNaznParamGuid = new Guid("2a73f7b8-05e7-410a-b22a-66498e315df4"); //Т_Назначение
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            BuiltInParameter hal = BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM; //смещ от ур

            #region Сбор элементов

            Logger.Log("Начинаем сбор элементов",1);

            Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
#if R2022
            List<FloorType> list1 = ((IEnumerable<Element>)new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType)))
                .Where<Element>((Func<Element, bool>)(f => f.Category.Id.IntegerValue.Equals(-2000032)))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString() != null))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString().Contains("Пол")))
                .Cast<FloorType>().OrderBy<FloorType, string>((Func<FloorType, string>)(f => ((Element)f).Name), (IComparer<string>)new AlphanumComparatorFastString())
                .ToList<FloorType>(); //типы полов
#else
            List<FloorType> list1 = ((IEnumerable<Element>)new FilteredElementCollector(doc)
               .OfClass(typeof(FloorType)))
               .Where<Element>((Func<Element, bool>)(f => f.Category.Id.Value.Equals(-2000032)))
               .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString() != null))
               .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString().Contains("Пол")))
               .Cast<FloorType>().OrderBy<FloorType, string>((Func<FloorType, string>)(f => ((Element)f).Name), (IComparer<string>)new AlphanumComparatorFastString())
               .ToList<FloorType>(); //типы полов
#endif
            if (list1.Count == 0)
            {
                string info1txt = "Ошибка! В проекте отсутствуют типы полов. Необходимо наличие перекрытий со значением параметра Группа модели, содержащим слово Пол.";
                var info1 = new InfoWindow400(info1txt); info1.ShowDialog();
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/poly/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                Logger.Log("Отсутствуют полы. Завершение работы.", 3);
                return Result.Cancelled;
            }

            //анализ текущей выборки
            Logger.Log("Анализ текущей выборки",1);
            List<Room> roomList = new List<Room>();
            roomList = Floors.GetRoomsFromCurrentSelection(doc, selection); //получаем комнаты из текущей выборки
            if (roomList.Count == 0) //запускаем выбор элементов если ничего не выбрано
            {
                RoomSelectionFilter roomSelectionFilter = new RoomSelectionFilter();
                IList<Reference> referenceList;
                try
                {
                    referenceList = selection.PickObjects((ObjectType)1, (ISelectionFilter)roomSelectionFilter, "Выберите помещения");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
                {
                    Logger.Log("Отменено: "+ex.Message+". Завершение работы.",3); return Result.Cancelled;
                }
                foreach (Reference reference in (IEnumerable<Reference>)referenceList)
                    roomList.Add(doc.GetElement(reference) as Room);
            }

            if(roomList.Count<1) { Logger.Log("Элементы не выбраны. Завершение работы.", 3); return Result.Cancelled; }
#endregion

            Logger.Log("Элементы собраны. Выбор сценария",1);

            #region Диалог
            var viewModel = new FloorViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in DBCommandName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<FloorViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new FloorWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } 
            else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message, 4); }
            #endregion

            string typename = viewModel.typename;
            FloorType ft = list1[0];
            foreach (var f in list1)
            {
                if(f.Name == typename) { ft = f; }
            }
            double offset = 0;
            double.TryParse(viewModel.offset, out offset);
            offset = offset / 304.8;

            int created = 0;

            #region Основной код
            Logger.Log("Создание полов",1);
                        
            using (TransactionGroup transactionGroup = new TransactionGroup(doc))
            {
                using (Transaction transaction = new Transaction(doc))
                {
                    transactionGroup.Start("Создание пола");
                    Logger.Log("Открываем транзакцию",1);
                    foreach (Room room1 in roomList)
                    {
                        Room room = room1;
                        Level level = ((SpatialElement)room).Level;
                        Logger.Log("Комната " + room.Id.ToString(), 1);

                        if (level != null)
                        {
                            Logger.Log("ищем границы", 2);
                            IList<IList<BoundarySegment>> boundarySegments =
                                ((SpatialElement)room).GetBoundarySegments(new SpatialElementBoundaryOptions());
                            Element elem = (Element)room;
                            LocationPoint lp = (LocationPoint)elem.Location;

                            // --- 1. Собираем все контуры в список CurveLoop ---
                            IList<CurveLoop> curveLoops = new List<CurveLoop>();
                            foreach (IList<BoundarySegment> segList in boundarySegments)
                            {
                                CurveLoop loop = new CurveLoop();
                                foreach (BoundarySegment seg in segList)
                                {
                                    loop.Append(seg.GetCurve());
                                }
                                curveLoops.Add(loop);
                            }

                            Logger.Log("полы в проекте", 2);
#if R2022
                            List<Floor> list2 = new FilteredElementCollector(doc)
                                .OfClass(typeof(Floor))
                                .Cast<Floor>()
                                .Where(f => f.LevelId == room.LevelId)
                                .Where(f => f.Category.Id.IntegerValue == -2000032)
                                .Where(f => f.FloorType.get_Parameter(gm).AsString().Contains("Пол"))
                                .OrderBy(f => f.Name)
                                .ToList();
#else
                            List<Floor> list2 = new FilteredElementCollector(doc)
                                .OfClass(typeof(Floor))
                                .Cast<Floor>()
                                .Where(f => f.LevelId == room.LevelId)
                                .Where(f => f.Category.Id.Value == -2000032)
                                .Where(f => f.FloorType.get_Parameter(gm).AsString().Contains("Пол"))
                                .OrderBy(f => f.Name)
                                .ToList();
#endif
                            Logger.Log("колво " + list2.Count.ToString(), 2);

                            // --- Удаление старого пола (без изменений) ---
                            transaction.Start("Удаление старого пола");
                            Logger.Log("   Удаляем старый пол", 1);
                            Solid solid1 = null;
                            GeometryElement geometry = elem.get_Geometry(new Options());
                            List<Solid> solids1 = GetSolidsOfElement(geometry);
                            solid1 = solids1[0];
                            if (solid1 == null) { Logger.Log("      прервано", 1); break; }

                            foreach (Floor floor in list2)
                            {
                                Solid solid2 = null;
                                Element elem2 = (Element)floor;
                                GeometryElement geometry2 = elem2.get_Geometry(new Options());
                                List<Solid> solids2 = GetSolidsOfElement(geometry2);
                                solid2 = solids2[0];
                                if (solid2 == null) { Logger.Log("      прервано", 1); break; }

                                Solid solid3 = BooleanOperationsUtils.ExecuteBooleanOperation(
                                    SolidUtils.CreateTransformed(solid2, Transform.CreateTranslation(new XYZ(0, 0, 625.0 / 381.0))),
                                    solid1,
                                    BooleanOperationsType.Intersect);

                                if (solid3 != null && solid3.Volume != 0.0)
                                    doc.Delete(floor.Id);
                            }
                            transaction.Commit();
                            Logger.Log("   Старый пол удален;", 1);

                            // --- Создание нового пола через Floor.Create ---
                            transaction.Start("Создание пола");
                            Logger.Log("   Создаем новый пол", 1);

                            // Используем перегрузку с параметром offset
                            Floor floor1 = Floor.Create(doc, curveLoops, ft.Id, level.Id, false, null, offset);

                            Element felem = (Element)floor1;

                            // Дополнительная установка параметров отделки (как в исходном коде)
                            Parameter roomParam = felem.get_Parameter(NFinishRoomParamGuid);
                            Parameter roomParam2 = felem.LookupParameter(NFinishElemNaznParam);
                            Parameter roomParam3 = felem.get_Parameter(NFinishElemGroupParamGuid);

                            string roomName = room.get_Parameter(roomNameParam).AsString();
                            string roomNazn = room.get_Parameter(roomNaznParam)?.AsString() ?? "";
                            string roomGroup = room.get_Parameter(NFinishRoomGroupParamGuid)?.AsInteger().ToString() ?? "";

                            roomParam?.Set(roomName);
                            roomParam2?.Set(roomNazn);
                            roomParam3?.Set(roomGroup);

                            // Обработка предупреждений (как было)
                            FailureHandlingOptions failureHandlingOptions = transaction.GetFailureHandlingOptions();
                            failureHandlingOptions.SetFailuresPreprocessor(new FloorIntersectionWarningSwallower());
                            transaction.SetFailureHandlingOptions(failureHandlingOptions);

                            transaction.Commit();
                            created++;
                            Logger.Log("   Новый пол создан", 1);

                            // --- Весь код, связанный с вырезанием проемов, удалён ---
                            // (отверстия уже учтены в curveLoops)
                        }
                    }
                    
                    transactionGroup.Assimilate();
                }
            }
#endregion
            if (created > 0)
            {
                if (created == 1) { var info1 = new InfoWindow280("Успешно!\nПол в выбранном помещении создан."); info1.ShowDialog(); }
                else { var info1 = new InfoWindow280("Успешно!\nСозданы полы в количестве " + created.ToString() + " шт."); info1.ShowDialog(); }
            }
            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
        private static List<Room> GetRoomsFromCurrentSelection(Autodesk.Revit.DB.Document doc, Autodesk.Revit.UI.Selection.Selection sel)
        {
            ICollection<ElementId> elementIds = sel.GetElementIds();
            List<Room> currentSelection = new List<Room>();
            foreach (ElementId elementId in (IEnumerable<ElementId>)elementIds)
            {
#if R2022
                if (doc.GetElement(elementId) is Room && doc.GetElement(elementId).Category != null && doc.GetElement(elementId).Category.Id.IntegerValue.Equals(-2000160))
                    currentSelection.Add(doc.GetElement(elementId) as Room);
#else
                if (doc.GetElement(elementId) is Room && doc.GetElement(elementId).Category != null && doc.GetElement(elementId).Category.Id.Value.Equals(-2000160))
                    currentSelection.Add(doc.GetElement(elementId) as Room);
#endif
            }
            return currentSelection;
        }

        private static List<Solid> GetSolidsOfElement(GeometryElement geoElem)
        {
            List<Solid> solids = new List<Solid>();

            foreach (GeometryObject geoObj in geoElem)
            {
                if (geoObj is Solid)
                {
                    Solid solid = geoObj as Solid;
                    if (solid == null) continue;
                    if (solid.Volume == 0) continue;
                    solids.Add(solid);
                    continue;
                }
                if (geoObj is GeometryInstance)
                {
                    GeometryInstance geomIns = geoObj as GeometryInstance;
                    GeometryElement instGeoElement = geomIns.GetInstanceGeometry();
                    List<Solid> solids2 = GetSolidsOfElement(instGeoElement);
                    solids.AddRange(solids2);
                }
            }
            return solids;
        }
    }
    internal class FloorIntersectionWarningSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (FailureMessageAccessor failureMessage in (IEnumerable<FailureMessageAccessor>)failuresAccessor.GetFailureMessages())
            {
                if ((FailureDefinitionId)BuiltInFailures.OverlapFailures.FloorsOverlap== failureMessage.GetFailureDefinitionId())
                    failuresAccessor.DeleteWarning(failureMessage);
                else if ((FailureDefinitionId)BuiltInFailures.InaccurateFailures.InaccurateSketchLine==failureMessage.GetFailureDefinitionId())
                    failuresAccessor.DeleteWarning(failureMessage);
            }
            return (FailureProcessingResult)0;
        }
    }
    public class RoomSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element.Category.Name == "Помещения") return true; else return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
}
