using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TNovCommon;

namespace TNovFinishing
{
    public class FloorViewModel : INotifyPropertyChanged
    {
        private string _offset = "0";
        public string offset { get => _offset; set { _offset = value; OnPropertyChanged(); } }

        [JsonIgnore] public ObservableCollection<string> typelist { get; set; }
        private string _typename;
        public string typename { get { return _typename; } set { _typename = value; OnPropertyChanged(); } }

        private int _typenum = 0;
        public int typenum { get => _typenum; set { _typenum = value; OnPropertyChanged(); } }
        public FloorViewModel()
        {
            Param();
        }
        private void Param()
        {
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
#if R2022
            List<FloorType> list1 = ((IEnumerable<Element>)new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(FloorType)))
                .Where<Element>((Func<Element, bool>)(f => f.Category.Id.IntegerValue.Equals(-2000032)))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString() != null))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString().Contains("Пол")))
                .Cast<FloorType>().OrderBy<FloorType, string>((Func<FloorType, string>)(f => ((Element)f).Name), (IComparer<string>)new AlphanumComparatorFastString())
                .ToList<FloorType>(); //типы полов
#else
            List<FloorType> list1 = ((IEnumerable<Element>)new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(FloorType)))
                .Where<Element>((Func<Element, bool>)(f => f.Category.Id.Value.Equals(-2000032)))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString() != null))
                .Where<Element>((Func<Element, bool>)(f => f.get_Parameter(gm).AsString().Contains("Пол")))
                .Cast<FloorType>().OrderBy<FloorType, string>((Func<FloorType, string>)(f => ((Element)f).Name), (IComparer<string>)new AlphanumComparatorFastString())
                .ToList<FloorType>(); //типы полов
#endif
            typelist = new ObservableCollection<string> { };
            foreach (Element e in list1) { typelist.Add(e.Name); }
            typename = typelist[typenum];
        }


        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler HideRequest;
        private void RaiseHideRequest()
        {
            HideRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler ShowRequest;
        private void RaiseShowRequest()
        {
            ShowRequest?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
