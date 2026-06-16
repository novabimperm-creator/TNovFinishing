using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNovFinishing
{
    public class TNovFloorCeilingUpdater : IUpdater
    {
        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        
        public TNovFloorCeilingUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("3C1F3F47-7B1C-4A7E-8A0A-1A1B1C1D1E1F"));
        }
        string GetTNazn(string Nazn, string Name)
        {
            string TNazn = "";
            if (Nazn.Contains("Жил")) TNazn = Nazn;
            else if (Nazn.Contains("Технич"))
            {
                if (Name.Contains("Лестн") || Name.Contains("лестн")) TNazn = "Лестница";
                else TNazn = "Техническое";
            }
            else if (Nazn.Contains("Лестн")) TNazn = "Лестница";
            else if (Nazn.Contains("Кладов")) TNazn = "Кладовые";
            else if (Nazn.Contains("Встроен")) TNazn = "МОП";
            else if (Nazn.Contains("Парк")) TNazn = "МОП";
            else if (Nazn.Contains("МОП"))
            {
                if (Name.Contains("Лестн") || Name.Contains("лестн")) TNazn = "Лестница";
                else if (Name.Contains("Кладов")) TNazn = "Кладовые";
                else if (Name.Contains("Электр")) TNazn = "Техническое";
                else if (Name.Contains("связи")) TNazn = "Техническое";
                else if (Name.Contains("Технич")) TNazn = "Техническое";
                else if (Name.Contains("Техпом")) TNazn = "Техническое";
                else if (Name.Contains("кондиц")) TNazn = "Техническое";
                else if (Name.Contains("ИТП")) TNazn = "Техническое";
                else if (Name.Contains("Котельная")) TNazn = "Техническое";
                else if (Name.Contains("Пульт")) TNazn = "Техническое";
                else if (Name.Contains("Венткамера")) TNazn = "Техническое";
                else TNazn = "МОП";
            }
            else TNazn = "Коммерция";
            return TNazn;
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            ICollection<ElementId> addedIds = data.GetAddedElementIds();
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();

            // Объединяем все измененные элементы
            var allElementIds = new HashSet<ElementId>(addedIds);
            allElementIds.UnionWith(modifiedIds);

            if (!allElementIds.Any()) return;

            string docName = doc.Title.ToString();
            if (docName.Contains("-АР") || docName.Contains("_АР") || docName.Contains("-АР-") || docName.Contains("_ПОФ") || docName.Contains("-ПОФ-"))
            {
                foreach (ElementId elementId in allElementIds)
                {
                    Element element = doc.GetElement(elementId);
                    if (element == null) continue;

                    // Проверяем категорию элемента
                    if (IsFloorOrCeiling(element))
                    {
                        bool parsAreEmpty = false; //проверяем, что параметры не заполнены (запуск только если любой из параметров пустой)
                        List<string> paramNames = new List<string>() { "N_Отделка.Помещение", "N_Отделка.Помещение и номер", "Отделка.Помещение.Назначение", "N_Отделка.ГруппаТекст" };
                        foreach (string paramName in paramNames)
                        {
                            Parameter param = element.LookupParameter(paramName);
                            if (param != null)
                            {
                                if (param.HasValue)
                                {
                                    if (param.AsString().Length < 1) { parsAreEmpty = true; break; }
                                }
                                else { parsAreEmpty = true; break; }
                            }
                        }

                        if (parsAreEmpty) UpdateElementRoomParameter(doc, element);
                    }
                }
            }
                

                
        }

        private bool IsFloorOrCeiling(Element element)
        {
            return element.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Floors ||
                   element.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Ceilings;
        }

        private void UpdateElementRoomParameter(Document doc, Element element)
        {
            //параметры
            Guid NFinishRoomParamGuid = new Guid("8b9d4aff-a6c8-4ad5-b0f5-442f2b87c765"); //N_Отделка.Помещение
            Guid NFinishRoomAndNumberParamGuid = new Guid("3855f341-c148-4170-914e-eb1d75fd1ba0"); //N_Отделка.Помещение и номер
            string NFinishElemNaznParam = "Отделка.Помещение.Назначение";
            Guid NFinishElemGroupParamGuid = new Guid("60e4ba60-55ca-4922-8ce7-22a6c43c95c2"); //N_Отделка.ГруппаТекст
            BuiltInParameter roomNameParam = BuiltInParameter.ROOM_NAME;
            BuiltInParameter roomNaznParam = BuiltInParameter.ROOM_DEPARTMENT;
            Guid NFinishRoomGroupParamGuid = new Guid("76144285-f586-4eb7-af04-e4ad9902f67a"); //N_Отделка.Группа
            Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
            Guid TPolozhParamGuid = new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"); //Т_Положение
            Guid TNaznParamGuid = new Guid("2a73f7b8-05e7-410a-b22a-66498e315df4"); //Т_Назначение
            BuiltInParameter roomNumberParam = BuiltInParameter.ROOM_NUMBER;

            Room room = FindRoomForElementFast(doc, element);

            if (room != null)
            {
                Parameter roomParam = element.get_Parameter(NFinishRoomParamGuid);
                Parameter roomParam2 = element.LookupParameter(NFinishElemNaznParam);
                Parameter roomParam3 = element.get_Parameter(NFinishElemGroupParamGuid);
                Parameter roomParam4 = element.get_Parameter(NFinishRoomAndNumberParamGuid);

                string roomName = room.get_Parameter(roomNameParam).AsString();
                string roomNazn = room.get_Parameter(roomNaznParam)?.AsString() ?? "";
                string roomGroup = room.get_Parameter(NFinishRoomGroupParamGuid)?.AsInteger().ToString() ?? "";
                string roomNumber = room.get_Parameter(roomNumberParam)?.AsString() ?? "";
                string roomNameAndNumber = roomName + " (" + roomNumber + ")";

                string currentValue = roomParam?.AsString();
                if (currentValue != roomName)
                {
                    roomParam.Set(roomName);
                }

                string currentValue2 = roomParam2?.AsString();
                if (currentValue2 != roomNazn)
                {
                    roomParam2.Set(roomNazn);
                }

                string currentValue3 = roomParam3?.AsString();
                if (currentValue3 != roomGroup)
                {
                    roomParam3.Set(roomGroup);
                }

                string currentValue4 = roomParam4?.AsString();
                if (currentValue4 != roomNameAndNumber)
                {
                    roomParam4.Set(roomNameAndNumber);
                }

                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, element))
                {
                    if (element.get_Parameter(NTParamsNotSetParamGuid).AsDouble() != 1)
                    {
                        string value = GetTNazn(roomNazn, roomName);
                        if (Param.ParamExistByGuid(TPolozhParamGuid, element))
                        {
                            element.get_Parameter(TPolozhParamGuid).Set(value);
                        }
                        if (Param.ParamExistByGuid(TNaznParamGuid, element))
                        {
                            element.get_Parameter(TNaznParamGuid).Set(value);
                        }
                    }
                }
            }
        }

        private Room FindRoomForElementFast(Document doc, Element element)
        {
            try
            {
                // Получаем уровень элемента
                Level elementLevel = doc.GetElement(element.LevelId) as Level;
                if (elementLevel == null) return null;

                // Получаем фазу элемента
                Phase phase = doc.GetElement(element.CreatedPhaseId) as Phase;
                ElementId phaseId = phase?.Id;

                // Получаем BoundingBox элемента
                BoundingBoxXYZ elementBBox = element.get_BoundingBox(null);
                if (elementBBox == null) return null;

                // Вычисляем центр элемента
                XYZ center = new XYZ(
                    (elementBBox.Min.X + elementBBox.Max.X) / 2,
                    (elementBBox.Min.Y + elementBBox.Max.Y) / 2,
                    (elementBBox.Min.Z + elementBBox.Max.Z) / 2
                );

                // Для потолков смещаем точку вниз, для полов - вверх
                double verticalOffset = element is Ceiling ? -0.5 : 0.5;
                XYZ testPoint = new XYZ(center.X, center.Y, center.Z + verticalOffset);

                // Метод 1: Используем встроенный метод Revit для поиска помещения по точке
                Room room = doc.GetRoomAtPoint(testPoint);
                if (room != null && IsRoomValid(room, elementLevel, phaseId))
                    return room;

                // Метод 2: Если не нашли, пробуем точку без смещения
                room = doc.GetRoomAtPoint(center);
                if (room != null && IsRoomValid(room, elementLevel, phaseId))
                    return room;

                // Метод 3: Ищем помещение с наибольшим пересечением по BoundingBox
                return FindRoomByBoundingBoxIntersection(doc, element, elementLevel, phaseId);
            }
            catch
            {
                return null;
            }
        }

        private bool IsRoomValid(Room room, Level elementLevel, ElementId phaseId)
        {
            return room.LevelId == elementLevel.Id &&
                   room.Area > 0 &&
                   (phaseId == null || room.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == phaseId);
        }

        private Room FindRoomByBoundingBoxIntersection(Document doc, Element element, Level elementLevel, ElementId phaseId)
        {
            try
            {
                // Получаем ограничивающую рамку элемента
                BoundingBoxXYZ elementBBox = element.get_BoundingBox(null);
                if (elementBBox == null) return null;

                // Получаем все помещения на том же уровне
                List<Room> roomsOnLevel = GetRoomsOnSameLevel(doc, elementLevel, phaseId);

                // Находим помещение с наибольшей площадью пересечения по BoundingBox
                Room bestRoom = null;
                double maxIntersectionArea = 0;

                foreach (Room room in roomsOnLevel)
                {
                    BoundingBoxXYZ roomBBox = room.get_BoundingBox(null);
                    if (roomBBox == null) continue;

                    double intersectionArea = CalculateBoundingBoxIntersectionArea(roomBBox, elementBBox);
                    if (intersectionArea > maxIntersectionArea)
                    {
                        maxIntersectionArea = intersectionArea;
                        bestRoom = room;
                    }
                }

                return bestRoom;
            }
            catch
            {
                return null;
            }
        }

        private List<Room> GetRoomsOnSameLevel(Document doc, Level elementLevel, ElementId phaseId)
        {
            List<Room> rooms = new List<Room>();

            try
            {
                // Используем более быстрый фильтр для получения помещений
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfCategory(BuiltInCategory.OST_Rooms);

                foreach (Element elem in collector)
                {
                    Room room = elem as Room;
                    if (room != null &&
                        room.LevelId == elementLevel.Id &&
                        room.Area > 0 &&
                        (phaseId == null || room.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == phaseId))
                    {
                        rooms.Add(room);
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустой список
            }

            return rooms;
        }

        private double CalculateBoundingBoxIntersectionArea(BoundingBoxXYZ roomBBox, BoundingBoxXYZ elementBBox)
        {
            // Вычисляем пересечение по X и Y
            double xOverlap = Math.Max(0,
                Math.Min(roomBBox.Max.X, elementBBox.Max.X) -
                Math.Max(roomBBox.Min.X, elementBBox.Min.X));

            double yOverlap = Math.Max(0,
                Math.Min(roomBBox.Max.Y, elementBBox.Max.Y) -
                Math.Max(roomBBox.Min.Y, elementBBox.Min.Y));

            return xOverlap * yOverlap; // Приблизительная площадь пересечения
        }

        public string GetAdditionalInformation() => "Обновляет имя помещения для перекрытий и потолков";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => "TNovFloorCeilingUpdater";
    }

}
