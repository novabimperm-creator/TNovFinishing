using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Outline = Autodesk.Revit.DB.Outline;
using View = Autodesk.Revit.DB.View;
using System.Threading;
using System.Windows.Threading;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows;
using TNovCommon;


namespace TNovFinishing
{
    
    [Transaction(TransactionMode.Manual)]
    public class FloorImages : IExternalCommand
    {
        private TNovProgressBar floorimagesProgressBar;
        private void ThreadStartingPoint()
        {
            this.floorimagesProgressBar = new TNovProgressBar();
            this.floorimagesProgressBar.Show();
            Dispatcher.Run();
        }
        
        bool ElementNameEndsWithJpg(Element e)
        {
            string s = e.Name;

            return 3 < s.Length && s.EndsWith(".jpg");
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string imgPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/");

            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Ведомость полов";
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

            

            //запрещенные символы
            string rSymbols = @"<>:""/\|?*"; List<string> badNames = new List<string>();

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели

            #region Сбор элементов
            Logger.Log("Сбор элементов",1);

            Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
#if R2022
            List<FloorType> floors = ((IEnumerable<Element>)new FilteredElementCollector(doc) //типы полов системных
                .OfClass(typeof(FloorType)))
                .Where<Element>((Func<Element, bool>)(f => f.Category.Id.IntegerValue.Equals(-2000032)))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString() != null))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString().Contains("Пол")))
                .Cast<FloorType>().OrderBy<FloorType, string>((Func<FloorType, string>)(f => ((Element)f).Name), (IComparer<string>)new AlphanumComparatorFastString())
                .ToList<FloorType>(); //типы полов
#else
            List<FloorType> floors = ((IEnumerable<Element>)new FilteredElementCollector(doc) //типы полов системных
                .OfClass(typeof(FloorType)))
                .Where<Element>((Func<Element, bool>)(f => f.Category.Id.Value.Equals(-2000032)))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString() != null))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString().Contains("Пол")))
                .Cast<FloorType>().OrderBy<FloorType, string>((Func<FloorType, string>)(f => ((Element)f).Name), (IComparer<string>)new AlphanumComparatorFastString())
                .ToList<FloorType>(); //типы полов
#endif
            List<FamilySymbol> floorsFI = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors)   //типы полов семействами
                                                                         .WhereElementIsElementType()
                                                                         .OfClass(typeof(FamilySymbol))
                                                                         .Cast<FamilySymbol>()
                                                                         .ToList();

            List<ViewDrafting> viewDraftings = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting))
                .WhereElementIsNotElementType()
                .Cast<ViewDrafting>()
                .ToList();

            
            if (floors.Count == 0&&floorsFI.Count == 0)
            {
                string info1txt = "Ошибка! В проекте отсутствуют типы полов. Необходимо наличие перекрытий со значением параметра Группа модели, содержащим слово Пол.";
                var info1 = new InfoWindow400(info1txt); info1.ShowDialog();
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/poly/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Cancelled;
            }

            List<Element> list1 = new List<Element>();
            foreach (var f in floors)
            {
                Element e = doc.GetElement(f.Id); list1.Add(e);
            }
            foreach (var f in floorsFI)
            {
                Element e = doc.GetElement(f.Id); list1.Add(e);
            }
#endregion

            bool unhandledError = false;

            #region Основной код. Эскизы
            using (Transaction transaction1 = new Transaction(doc))
            {
                try
                {
                    transaction1.Start("TNov - сформировать эскизы");
                    Logger.Log("Открываем транзакцию 1 (эскизы)", 1);

                    foreach (var dView in viewDraftings)
                    {
                        if (dView.Name.StartsWith("Пол_Тип"))
                        {
                            bool viewCatParamExist = Param.ParamExist("Орг.КатегорияВида", dView);
                            if (!viewCatParamExist) continue;
                            else
                            {
                                if (dView.LookupParameter("Орг.КатегорияВида").HasValue)
                                {
                                    bool isViewRD = dView.LookupParameter("Орг.КатегорияВида").AsString().Contains("Стадия Р");
                                    if (!isViewRD) continue;
                                }
                                else continue;
                            }

                            //проверка имени вида
                            bool badName = false;
                            foreach (char c in rSymbols)
                            {
                                if (dView.Name.Contains(c))
                                {
                                    badNames.Add(dView.Name);
                                    Logger.Log("Плохое имя вида: " + dView.Name, 1);
                                    badName = true;
                                    break;
                                }
                            }
                            if (badName) continue;

                            string name = dView.Name.Replace("Пол_Тип", "");
                            name = name.TrimStart();
                            name = "Пол" + name;
                            string dViewName = dView.Name.Replace('.', '-');
                            //экспортируем изображение в файл
                            Logger.Log("Экспортируем изображение " + dView.Name + " в файл", 2);
                            IList<ElementId> ImageExportList = new List<ElementId>();

                            ImageExportList.Add(dView.Id);

                            var BilledeExportOptions = new ImageExportOptions
                            {
                                ZoomType = ZoomFitType.FitToPage,
                                PixelSize = 1024,
                                FilePath = imgPath,
                                FitDirection = FitDirectionType.Horizontal,
                                HLRandWFViewsFileType = ImageFileType.JPEGLossless,
                                ImageResolution = ImageResolution.DPI_600,
                                ExportRange = ExportRange.SetOfViews,
                            };

                            BilledeExportOptions.SetViewsAndSheets(ImageExportList);

                            doc.ExportImage(BilledeExportOptions);
                            string imgPath2 = imgPath + " - Чертежный вид - " + dViewName + ".jpg";
                            string imgPath3 = imgPath + name + ".jpg";
                            File.Move(imgPath2, imgPath3);

                            //ищем существующее изображение в проекте и удаляем его
                            string searchName = name;
                            ICollection<ElementId> imagesToDelete = new List<ElementId>();
                            FilteredElementCollector col = new FilteredElementCollector(doc).WhereElementIsElementType();
                            foreach (Element e in col)
                            {
                                if (ElementNameEndsWithJpg(e))
                                {
                                    if (e.Name.Contains(searchName))
                                    {

                                        Logger.Log("Удаляем существующее изображение", 2);
                                        imagesToDelete.Add(e.Id);
                                        doc.Delete(imagesToDelete);
                                        break;
                                    }

                                }
                            }

                            //импортируем новое изображение
                            Logger.Log("Импортируем изображение", 2);
                            ImageTypeOptions imageTypeOptions = new ImageTypeOptions(imgPath3, false, ImageTypeSource.Import);
                            imageTypeOptions.Resolution = 300;
                            ImageType imageType = ImageType.Create(doc, imageTypeOptions);

                            //удаляем файл
                            File.Delete(imgPath3);
                        }
                    }

                    transaction1.Commit();
                    Logger.Log("Закрываем транзакцию 1", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;

                }
            }
            #endregion

            int allcount = list1.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.floorimagesProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.floorimagesProgressBar.value.Text = PBCount.ToString()));
            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.floorimagesProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.floorimagesProgressBar.maxvalue.Text = allcount.ToString()));

            #region Основной код. Назначение эскизов
            using (Transaction transaction2 = new Transaction(doc))
            {
                try 
                { 
                    transaction2.Start("TNov - назначить эскизы");
                    Logger.Log("Открываем транзакцию 2 (назначить эскизы)",1);

                    List<ImageType> imageTypes = new FilteredElementCollector(doc).OfClass(typeof(ImageType))
                .WhereElementIsElementType()
                .Cast<ImageType>()
                .ToList();

                    if(floors.Count > 0) //системные типы полов
                    {
                        foreach (FloorType floor in floors)
                        {
                            PBCount++;
                            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.floorimagesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.floorimagesProgressBar.value.Text = PBCount.ToString()));

                            Element elem = doc.GetElement(floor.Id);

                            Logger.Log("Тип пола " + elem.Name, 2);

                            string floorTypeMarkValue = elem.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();

                            string searchName = "Пол" + floorTypeMarkValue;

                            foreach (ImageType e in imageTypes)
                            {

                                if (e.Name.Contains(searchName))
                                {

                                    var typeImageParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_IMAGE);
                                    try
                                    {
                                        typeImageParam.Set(e.Id);
                                        Logger.Log("   Элемент " + elem.Id + ": изображение типа обновлено успешно",2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("   Элемент " + elem.Id + " Ошибка: " + ex.Message,4);
                                    }
                                    break;
                                }


                            }


                        }
                    }

                    if (floorsFI.Count > 0) //типы полов семействами
                    {
                        foreach (FamilySymbol floor in floorsFI)
                        {
                            PBCount++;
                            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.floorimagesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.floorimagesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.floorimagesProgressBar.value.Text = PBCount.ToString()));

                            List<FamilyInstance> elems = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors)   //полы семействами данного типа
                                                                             .WhereElementIsNotElementType()
                                                                             .OfClass(typeof(FamilyInstance))
                                                                             .Cast<FamilyInstance>()
                                                                             .Where(e => e.Symbol.Id == floor.Id)
                                                                             .ToList();

                            Element elemT = doc.GetElement(floor.Id);

                            Logger.Log("Тип пола " + elemT.Name, 2);

                            string floorTypeMarkValue = elemT.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();

                            string searchName = "Пол" + floorTypeMarkValue;

                            foreach (FamilyInstance f in elems)
                            {
                                Element elem = doc.GetElement(f.Id);
                                foreach (ImageType e in imageTypes)
                                {

                                    if (e.Name.Contains(searchName))
                                    {

                                        var imageParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE);
                                        try
                                        {
                                            imageParam.Set(e.Id);
                                            Logger.Log("   Элемент " + elem.Id + ": изображение обновлено успешно", 2);
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Log("   Элемент " + elem.Id + " Ошибка: " + ex.Message,4);
                                        }
                                        break;
                                    }


                                }
                            }

                        

                        

                        


                        }
                    }

                    transaction2.Commit();
                    
                    Logger.Log("Закрываем транзакцию 2",1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;

                }
                finally
                {
                    CloseProgressBarSafely();
                }
            }
            #endregion

            if (badNames.Count > 0)
            {
                new InfoWindow280("В проекте есть чертежные виды Пол_Тип с недопустимыми символами (" +
                    rSymbols + ") в именах: " + string.Join(", ", badNames) + ". Эти виды не обработаны, переименуйте виды и перезапустите плагин.").ShowDialog();
            }

            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
        private void CloseProgressBarSafely()
        {
            if (floorimagesProgressBar != null &&
                floorimagesProgressBar.Dispatcher != null &&
                !floorimagesProgressBar.Dispatcher.HasShutdownStarted)
            {
                floorimagesProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (floorimagesProgressBar.IsLoaded)
                        floorimagesProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }

}
