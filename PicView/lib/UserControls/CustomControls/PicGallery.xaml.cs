﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static PicView.lib.ImageManager;
using static PicView.lib.Variables;

namespace PicView.lib.UserControls
{
    /// <summary>
    /// Interaction logic for PicGallery.xaml
    /// </summary>
    public partial class PicGallery : UserControl
    {
        public bool LoadComplete, isLoading, open;
        public event MyEventHandler PreviewItemClick, ItemClick;
        int current_page, total_pages, items_per_page, horizontal_items, vertical_items;
        const int picGalleryItem_Size = 230;

        //int next_page
        //{
        //    get {
        //        return current_page + 1 > total_pages ? total_pages : current_page + 1;
        //    }
        //}

        //int prev_page
        //{
        //    get
        //    {
        //        return current_page - 1 < 0 ? 1 : current_page - 1;
        //    }
        //}

        public PicGallery()
        {
            InitializeComponent();
            Loaded += PicGallery_Loaded;
            x2.MouseLeftButtonUp += X2_MouseLeftButtonUp;
        }

        private void X2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            FadeOut();
        }

        private void FadeOut()
        {
            var da = new DoubleAnimation()
            {
                To = 0,
                From = 1,
                Duration = TimeSpan.FromSeconds(0.4)
            };
            da.Completed += delegate
            {
                open = false;
                Visibility = Visibility.Collapsed;
            };
            BeginAnimation(UIElement.OpacityProperty, da);
        }

        private void PicGallery_Loaded(object sender, RoutedEventArgs e)
        {
            Scroller.PreviewMouseWheel += Scroller_MouseWheel;
        }

        private void Scroller_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            ScrollTo(e.Delta > 0);
        }

        internal void Calculate_Paging()
        {
            horizontal_items = (int)Math.Ceiling(Width / picGalleryItem_Size);
            vertical_items = (int)Math.Ceiling(Container.ActualHeight / picGalleryItem_Size);
            items_per_page = horizontal_items * vertical_items;
            total_pages = (int)Math.Floor((double)Pics.Count / items_per_page);
            current_page = (int)Math.Floor((double)FolderIndex / items_per_page);

            //if (!string.IsNullOrWhiteSpace(PagingText.Text))
            //    PagingText.Text = "";

            //for (int i = 0; i < total_pages; i++)
            //{
            //    if (i > 5 && i < (total_pages - 1))
            //        continue;
            //    PagingText.Text += i;
            //    PagingText.Text += " ";
            //}
            //ScrollTo();
        }


        /// <summary>
        /// Scrolls a page back or forth
        /// </summary>
        /// <param name="next"></param>
        /// <param name="end"></param>
        internal void ScrollTo(bool next, bool end = false)
        {
            if (end)
            {
                if (next)
                    Scroller.ScrollToRightEnd();
                else
                    Scroller.ScrollToLeftEnd();
            }
            else
            {
                if (next)
                    Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + picGalleryItem_Size);
                else
                    Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - picGalleryItem_Size);
            }
        }

        /// <summary>
        /// Scrolls to center of current item
        /// </summary>
        /// <param name="item">The index of picGalleryItem</param>
        internal void ScrollTo()
        {
            var x = (picGalleryItem_Size * horizontal_items) * current_page;
            Scroller.ScrollToHorizontalOffset(x);
        }

        async void Add(BitmapSource pic, string file, int index)
        {
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var item = new PicGalleryItem(pic, file);
                item.MouseLeftButtonUp += (s, x) => Click(index);
                Container.Children.Add(item);
            }));
        }

        internal void Load()
        {
            isLoading = true;
            var t = new Task(() =>
            {
                for (int i = 0; i < Pics.Count; i++)
                {
                    var file = Pics[i];
                    var pic = GetBitmapSourceThumb(file);
                    if (pic != null)
                    {
                        pic.Freeze();
                        Add(pic, file, i);
                    }

                    if (i == Pics.Count - 1)
                        LoadComplete = true;
                }
            });
            t.Start();
        }

        internal void Clear()
        {
            LoadComplete = isLoading = open = false;
            Container.Children.Clear();
        }

        internal void Sort()
        {
            var x = Container.Children.Cast<PicGalleryItem>();
        }

        internal void Click(int id)
        {
            PreviewItemClick(this, new MyEventArgs(id, null));
            var img = new Image()
            {
                Source = GetBitmapSourceThumb(Pics[id]),
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var border = new Border()
            {
                BorderThickness = new Thickness(1),
                BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderBrush"],
                Background = (SolidColorBrush)Application.Current.Resources["BackgroundColorBrush"]
            };
            border.Child = img;
            grid.Children.Add(border);
            var from = 230; // 230 = PicGalleryItem size
            //var to = new double[] { Width, Height };
            var to = new double[] { Application.Current.MainWindow.ActualWidth - 15, Application.Current.MainWindow.ActualHeight - 95 };
            //var to = new double[] { Application.Current.MainWindow.Width, Application.Current.MainWindow.Height};
            var da = new DoubleAnimation();
            da.From = from;
            da.To = to[0]; // Width
            da.Duration = TimeSpan.FromSeconds(.3);
            da.AccelerationRatio = 0.2;
            da.DecelerationRatio = 0.4;

            var da0 = new DoubleAnimation();
            da0.From = from;
            da0.To = to[1]; // Height
            da0.Duration = TimeSpan.FromSeconds(.3);
            da0.AccelerationRatio = 0.2;
            da0.DecelerationRatio = 0.4;

            da.Completed += delegate
            {
                ItemClick(this, new MyEventArgs(id, img.Source));
                grid.Children.Remove(border);
                Visibility = Visibility.Collapsed;
                picGallery.open = false;
            };


            img.BeginAnimation(Rectangle.WidthProperty, da);
            img.BeginAnimation(Rectangle.HeightProperty, da0);
        }
    }


    // Event

    public delegate void MyEventHandler(object source, MyEventArgs e);


    public class MyEventArgs : EventArgs
    {
        private int Id;
        private ImageSource img;
        public MyEventArgs(int Id, ImageSource img)
        {
            this.Id = Id;
            this.img = img;
        }
        public int GetId()
        {
            return Id;
        }

        public ImageSource GetImage()
        {
            return img;
        }
    }

}
