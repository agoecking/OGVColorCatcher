using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OGVColorCatcher.ViewModels.MainWindowViewModel;

namespace OGVColorCatcher.Services
{
    //public class RibbonColorService
    //{
    //    public void ReplaceRibbonRow(
    //        ObservableCollection<RibbonRowModel> ribbonRows,
    //        List<float[]> newRgbSamples)
    //    {
    //        if (newRgbSamples == null || newRgbSamples.Count == 0)
    //            return;

    //        // Garante que exista pelo menos 1 linha
    //        if (ribbonRows.Count == 0)
    //            ribbonRows.Add(new RibbonRowModel());

    //        var row = ribbonRows[0];
    //        row.Colors.Clear();

    //        foreach (var rgb in newRgbSamples)
    //        {
    //            if (rgb == null || rgb.Length < 3)
    //                continue;

    //            row.Colors.Add(new SolidColorBrush(
    //                Color.FromRgb((byte)rgb[0], (byte)rgb[1], (byte)rgb[2])
    //            ));
    //        }
    //    }
    //}
}
