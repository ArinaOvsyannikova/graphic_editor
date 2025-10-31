using System.Drawing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Collections;
using System.Drawing.Imaging;
using System.Reflection;
using System.IO;
using System.Net;
//using static System.Net.Mime.MediaTypeNames;

namespace paint
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            bm = new Bitmap(pictureBox1.Width, pictureBox1.Height); // Создание нового изображения
            g = Graphics.FromImage(bm); // Создание объекта Graphics для рисования на изображении
            g.Clear(Color.White); // Очистка изображения
            pictureBox1.Image = bm; // Установка изображения на PictureBox
            sfd.Filter = "JPEG ( *.jpeg)| *.jpeg|BMP (*.bmp)|*.bmp";
            ofd.Filter = "JPEG ( *.jpeg)| *.jpeg|BMP (*.bmp)|*.bmp";
        }
        Bitmap bm; // Объявление переменной для хранения изображения
        Graphics g; // Объявление переменной для работы с графикой

        bool paint = false; // Флаг для определения рисования
        Point px, py; // Точки для рисования
        Pen p = new Pen(Color.Black, 10); // Карандаш для рисования

        Pen selection_pen = new Pen(Color.Blue, 1);
        Pen erase = new Pen(Color.White, 10); // Ластик для стирания
        int index = 1; // Индекс для выбора инструмента
        int x, y, sX, sY, cX, cY; // Переменные для координат и размеров фигур
        ColorDialog cd = new ColorDialog(); // Диалоговое окно выбора цвета
        Color new_color; // Выбранный цвет
        string filename;

        private Rectangle selection_rect;
        Point selection_offset;


        private bool is_selection = false;
        private bool isDragging = false; //флаг на режим перетаскивания
        Bitmap selection_image, selection_bm;

        private Stack<Bitmap> undostackCrop = new Stack<Bitmap>();//хранение состоянии при обрезке
        private Stack<Bitmap> undoStack = new Stack<Bitmap>(); //хранение состоний изображения при отмене 
        private Stack<Bitmap> redoStack = new Stack<Bitmap>(); //повторе 

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            paint = true;
            py = e.Location; // Запоминание начальной точки
            cX = e.X; cY = e.Y; // Запоминание координат начала
            is_selection = true;
            ChekCrop();

            if (index != 7) // Если не выбран инструмент "Выделение"
            {
                SaveState();
            }
            if (index == 7 && selection_rect.Contains(e.Location))
            {
                if (!isDragging)
                {
                    SaveState();
                    selection_image = new Bitmap(selection_rect.Width, selection_rect.Height);

                    //рисуется выделенная область на новом изображении
                    using (Graphics g = Graphics.FromImage(selection_image))
                    { g.DrawImage(bm, new Rectangle(0, 0, selection_image.Width, selection_image.Height), selection_rect, GraphicsUnit.Pixel); }

                    g.FillRectangle(Brushes.White, selection_rect);
                    g.DrawRectangle(new Pen(Color.White, 1), selection_rect);
                    undostackCrop.Push((Bitmap)bm.Clone());
                    selection_offset = new Point(cX - selection_rect.X, cY - selection_rect.Y);

                    isDragging = true;
                }
                else
                {
                    if (selection_rect.Contains(e.Location))
                    { //сохраняем смещение отн верхнего левого угла выделенного изображения
                        if (isDragging)
                        {
                            selection_offset = new Point(cX - selection_rect.X, cY - selection_rect.Y);
                            pictureBox1.Invalidate();
                        }
                    }
                    else
                    {
                        //очистка
                        while (undostackCrop.Count > 0)
                        {
                            undostackCrop.Pop();
                        }
                        isDragging = false;
                    }
                }
            }
            else
            {
                if (isDragging && index == 7)
                {
                    bm = undostackCrop.Pop(); // Восстановление предыдущего состояния изображения из стека обрезки
                    g = Graphics.FromImage(bm); // Создание объекта Graphics для работы с изображением
                    pictureBox1.Image = bm; // Установка изображения на PictureBox
                    undostackCrop.Push((Bitmap)bm.Clone()); // Сохранение текущего состояния изображения в стек обрезки
                    g.DrawImage(selection_image, selection_rect); // Отрисовка выделенной области на изображении
                    g.DrawRectangle(new Pen(Color.White, 1), selection_rect); // Рисование белой рамки вокруг выделенной области

                }
                while (undostackCrop.Count > 0)
                {
                    undostackCrop.Pop();
                }
                selection_rect = Rectangle.Empty;
                isDragging = false;
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            paint = false;
            sX = x - cX; sY = y - cY; // Вычисление размеров фигуры

            if (index == 3) // Если выбран инструмент "Элипс"
            {
                g.DrawEllipse(p, cX, cY, sX, sY); // Рисование эллипса
            }
            if (index == 4) // Если выбран инструмент "Квадрат"
            {
                sX = Math.Abs(x - cX);
                sY = Math.Abs(y - cY);
                cX = Math.Min(cX, x);
                cY = Math.Min(cY, y);
                g.DrawRectangle(p, cX, cY, sX, sY); // Рисование квадрата
            }
            if (index == 5) // Если выбран инструмент "Прямая"
            {
                g.DrawLine(p, cX, cY, x, y); // Рисование прямой линии
            }
            //рисуем квадрат выделения
            if (index == 7 && !selection_rect.Contains(e.Location) && is_selection)
            {
                int X = Math.Min(py.X, e.X);
                int Y = Math.Min(py.Y, e.Y);
                int width = Math.Abs(e.X - py.X);
                int height = Math.Abs(e.Y - py.Y);
                selection_rect = new Rectangle(X, Y, width, height);

                selection_pen.DashStyle = DashStyle.Dash;
                undostackCrop.Push((Bitmap)bm.Clone()); // Сохранение текущего состояния изображения в стек 
                g.DrawRectangle(selection_pen, selection_rect);

                is_selection = false;
                pictureBox1.Invalidate();
            }
            if (isDragging)
            {
                selection_pen.DashStyle = DashStyle.Dash; // Установка штриховой линии для ручки выделения
                int X = x - selection_offset.X; // Вычисление координаты X левого верхнего угла перемещаемого прямоугольника выделения
                int Y = y - selection_offset.Y; // Вычисление координаты Y левого верхнего угла перемещаемого прямоугольника выделения
                selection_rect.Location = new Point(X, Y); // Обновление местоположения прямоугольника выделения

                bm = undostackCrop.Pop(); // Восстановление предыдущего состояния изображения из стека обрезки
                g = Graphics.FromImage(bm); // Создание объекта Graphics для работы с изображением
                pictureBox1.Image = bm; // Установка изображения на PictureBox
                undostackCrop.Push((Bitmap)bm.Clone()); // Сохранение текущего состояния изображения в стек обрезки

                g.DrawImage(selection_image, selection_rect); // Отрисовка выделенной области на изображении
                g.DrawRectangle(selection_pen, selection_rect); // Рисование рамки вокруг выделенной области
                pictureBox1.Invalidate(); // Обновление PictureBox
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (paint) // Если идет рисование
            {
                if (index == 1) // Если выбран инструмент "Карандаш"
                {
                    px = e.Location;

                    g.DrawLine(p, px, py); // Рисование линии
                    py = px;
                }
                if (index == 2) // Если выбран инструмент "Ластик"
                {
                    px = e.Location;
                    g.DrawLine(erase, px, py); // Стирание линии
                    py = px;
                }

                if (index == 8) // Если выбран инструмент "Пипетка"
                {
                    if (bm != null) // Проверяем, что изображение загружено
                    {
                        Color color = bm.GetPixel(e.X, e.Y); // Получаем цвет пикселя под курсором
                        pictureBox2.BackColor = color; // Устанавливаем цвет на PictureBox2 (палитра)
                        p.Color = color; // Устанавливаем цвет карандаша
                    }
                    index = 1;
                }
            }
            pictureBox1.Refresh(); // Обновление PictureBox
            x = e.X; y = e.Y; // Обновление координат мыши
            sX = e.X - cX; // Обновление размеров фигуры
            sY = e.Y - cY;
        }

        //кнопка карандаш ind 1
        private void button11_Click(object sender, EventArgs e)
        {
            index = 1;
        }
        //кнопка ластик ind 2
        private void button12_Click(object sender, EventArgs e)
        {
            index = 2;
        }
        // элипс ind 3
        private void button17_Click(object sender, EventArgs e)
        {
            index = 3;
        }
        // квадрат ind 4
        private void button18_Click(object sender, EventArgs e)
        {
            index = 4;
        }
        // прямая ind 5
        private void button16_Click(object sender, EventArgs e)
        {
            index = 5;
        }
        //для отображения рисовки 
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            Graphics ge = e.Graphics;


            if (paint)
            {
                if (index == 1)
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                }
                if (index == 2)
                {
                    erase.StartCap = LineCap.Round;
                    erase.EndCap = LineCap.Round;
                }
                if (index == 3)
                {
                    ge.DrawEllipse(p, cX, cY, sX, sY);
                }
                if (index == 4)
                {
                    if (x > cX && y > cY)
                    {
                        ge.DrawRectangle(p, cX, cY, sX, sY);
                    }
                    else if (x < cX && y < cY)
                    {
                        ge.DrawRectangle(p, x, y, cX - x, cY - y);
                    }
                    else if (x < cX && y > cY)
                    {
                        ge.DrawRectangle(p, x, cY, cX - x, sY);
                    }
                    else if (x > cX && y < cY)
                    {
                        ge.DrawRectangle(p, cX, y, sX, cY - y);
                    }
                }
                if (index == 5)
                {
                    ge.DrawLine(p, cX, cY, x, y);
                }
                if (index == 7 && !isDragging)
                {
                    if (x > cX && y > cY) // Если курсор находится в правом нижнем угле
                    {
                        selection_pen.DashStyle = DashStyle.Dash;
                        ge.DrawRectangle(selection_pen, cX, cY, sX, sY);
                    }
                    else if (x < cX && y < cY) // Если курсор находится в левом верхнем угле
                    {
                        selection_pen.DashStyle = DashStyle.Dash;
                        ge.DrawRectangle(selection_pen, x, y, cX - x, cY - y);
                    }
                    else if (x < cX && y > cY) // Если курсор находится в левом нижнем угле
                    {
                        selection_pen.DashStyle = DashStyle.Dash;
                        ge.DrawRectangle(selection_pen, x, cY, cX - x, sY);
                    }
                    else if (x > cX && y < cY) // Если курсор находится в правом верхнем угле
                    {
                        selection_pen.DashStyle = DashStyle.Dash;
                        ge.DrawRectangle(selection_pen, cX, y, sX, cY - y);
                    }
                }

                if (isDragging) // Если происходит перетаскивание
                {
                    selection_pen.DashStyle = DashStyle.Dash;
                    int X = x - selection_offset.X; // Вычисление координаты X левого верхнего угла перемещаемого прямоугольника выделения
                    int Y = y - selection_offset.Y; // Вычисление координаты Y левого верхнего угла перемещаемого прямоугольника выделения
                    selection_rect.Location = new Point(X, Y); // Обновление местоположения прямоугольника выделения

                    bm = undostackCrop.Pop(); // Восстановление предыдущего состояния изображения из стека обрезки
                    g = Graphics.FromImage(bm); // Создание объекта Graphics для работы с изображением
                    pictureBox1.Image = bm; // Установка изображения на PictureBox
                    undostackCrop.Push((Bitmap)bm.Clone()); // Сохранение текущего состояния изображения в стек обрезки
                    g.DrawImage(selection_image, selection_rect); // Отрисовка выделенной области на изображении
                    ge.DrawRectangle(selection_pen, selection_rect); // Рисование рамки вокруг выделенной области
                    pictureBox1.Invalidate(); // Обновление PictureBox
                }
            }
        }

        // выделение ind 7
        private void button15_Click(object sender, EventArgs e)
        {
            index = 7;
        }
        //отчистка
        private void button2_Click(object sender, EventArgs e)
        {
            SaveState(); // Сохранение текущего состояния изображения
            while (undostackCrop.Count > 0) // Очистка стека 
            {
                undostackCrop.Pop();
            }
           
            selection_rect = Rectangle.Empty; // Очистка прямоугольника выделения
          
            g.Clear(Color.White); // Очистка изображения

            undostackCrop.Push((Bitmap)bm.Clone()); // Сохранение текущего состояния изображения в стек обрезки           
            pictureBox1.Invalidate(); // Принудительное обновление PictureBox

        }
        //палитра
        private void button19_Click(object sender, EventArgs e)
        {
            cd.ShowDialog(); // Отображение диалогового окна выбора цвета
            new_color = cd.Color; // Получение выбранного цвета
            pictureBox2.BackColor = cd.Color; // Установка цвета на PictureBox
            p.Color = cd.Color; // Установка цвета для карандаша
        }
        //размер
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            p.Width = erase.Width = trackBar1.Value;
            p.StartCap = LineCap.Round;
        }
        //цвет 
        private void pictureBox3_Click(object sender, EventArgs e)
        {
            p.Color = ((PictureBox)sender).BackColor;
            pictureBox2.BackColor = ((PictureBox)sender).BackColor;

        }
        //заливка ind 6
        private void button13_Click(object sender, EventArgs e)
        {
            index = 6;
        }
        // Определяется метод set_Point, который преобразует координаты точки относительно размеров PictureBox.
        static Point set_Point(PictureBox pb, Point pt)
        {
            float px = 1f * pb.Width / pb.Width;
            float py = 1f * pb.Height / pb.Height;
            return new Point((int)(pt.X * px), (int)(pt.Y * py));

        }
        // Метод VAlidate проверяет пиксель изображения на соответствие старому цвету и заменяет его на новый цвет.
        private void VAlidate(Bitmap bm, Stack<Point> sp, int x, int y, Color Old_Color, Color New_Color)
        {
            Color cx = bm.GetPixel(x, y);
            if (cx == Old_Color)
            {
                sp.Push(new Point(x, y));
                bm.SetPixel(x, y, New_Color);
            }
        }
        // Метод Fill заполняет область изображения новым цветом, используя стек для отслеживания пикселей.
        public void Fill(Bitmap bm, int x, int y, Color New_Clr)
        {
            Color Old_Color = bm.GetPixel(x, y);
            Stack<Point> pixel = new Stack<Point>();
            pixel.Push(new Point(x, y));
            bm.SetPixel(x, y, New_Clr);
            if (Old_Color == New_Clr) { return; }

            while (pixel.Count > 0)
            {
                Point pt = (Point)pixel.Pop();
                if (pt.X > 0 && pt.Y > 0 && pt.X < bm.Width - 1 && pt.Y < bm.Height - 1)
                {
                    VAlidate(bm, pixel, pt.X - 1, pt.Y, Old_Color, New_Clr);
                    VAlidate(bm, pixel, pt.X, pt.Y - 1, Old_Color, New_Clr);
                    VAlidate(bm, pixel, pt.X + 1, pt.Y, Old_Color, New_Clr);
                    VAlidate(bm, pixel, pt.X, pt.Y + 1, Old_Color, New_Clr);

                }
            }
        }

        // При клике мышью на кнопку, если значение index равно 6, вызывается метод Fill для заполнения области изображения новым цветом.
        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            new_color = pictureBox2.BackColor;

            if (index == 6)
            {
                Point point = set_Point(pictureBox1, e.Location);
                Fill(bm, point.X, point.Y, new_color);
            }
            if (index != 7 && isDragging)
            {
                bm = undostackCrop.Pop();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                undostackCrop.Push((Bitmap)bm.Clone());
                g.DrawImage(selection_image, selection_rect);

                //очситка стека
                while (undostackCrop.Count > 0)
                {
                    undostackCrop.Pop();
                }
                selection_rect = Rectangle.Empty;

                isDragging = false;//чертов флаг
            }
        }

        //сохранить как
        private void button8_Click(object sender, EventArgs e)
        {
            ChekCrop();
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                filename = sfd.FileName;
                sfd.FileName = "";
                try
                {
                    // Проверяем расширение файла и сохраняем изображение в соответствующем формате
                    if (Path.GetExtension(filename).ToLower() == ".jpeg")
                    {
                        bm.Save(filename, ImageFormat.Jpeg);
                    }
                    else if (Path.GetExtension(filename).ToLower() == ".bmp")
                    {
                        bm.Save(filename, ImageFormat.Bmp);
                    }

                    MessageBox.Show("Изображение успешно сохранено.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении изображения: {ex.Message}");
                }
            }

        }
        //сохранить
        private void button7_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(filename))
            {
                // Если файл не открыт, открываем форму сохранения
                button8_Click(sender, e);
            }
            else
            {
                // Проверяем, существует ли файл
                if (System.IO.File.Exists(filename))
                {
                    // Файл существует, обновляем существующий файл
                    try
                    {
                        if (Path.GetExtension(filename).ToLower() == ".jpeg")
                        {
                            bm.Save(filename, ImageFormat.Jpeg);
                        }
                        else if (Path.GetExtension(filename).ToLower() == ".bmp")
                        {
                            bm.Save(filename, ImageFormat.Bmp);
                        }
                        MessageBox.Show("Файл успешно обновлен");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при обновлении файла: " + ex.Message);
                    }
                }
                else
                {
                    // Файл не существует, открываем форму сохранения
                    button8_Click(sender, e);
                }
            }
        }
        //открыть
        private void button9_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (undoStack.Count > 0)
                {
                    DialogResult result = MessageBox.Show("Вы хотите сохранить файл изображения?", "Сохранение файла", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        button7_Click(sender, e);// сохранение текущего состояния 
                    }
                    else if (result == DialogResult.No)
                    {
                        undoStack.Clear();
                    }
                    else
                    {
                        return;
                    }
                }
                string selectedFile = ofd.FileName;
                this.Text = Path.GetFileName(ofd.FileName);
                //bm = new Bitmap(selectedFile);
                // pictureBox1.Image = bm;
                filename = selectedFile;
                System.Drawing.Image image = System.Drawing.Image.FromFile(selectedFile);
                g.DrawImage(image, 0, 0);
                pictureBox1.Invalidate();
            }
        }
        //новый лист
        private void button10_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Хотите сохранить изменения в файле?", "Предупреждение", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                button7_Click(sender, e);
                Form1 newForm = new Form1();
                newForm.Show();
            }
            else if (result == DialogResult.No)
            {
                g.Clear(Color.White);
                pictureBox1.Image = bm;// Обновление изображения на PictureBox
                index = 1;
            }
            else if (result == DialogResult.Cancel)
            {
                return;
            }


        }
        //копировать
        private void button1_Click(object sender, EventArgs e)
        {
            if (selection_rect != Rectangle.Empty && index == 7)
            {
                bm = undostackCrop.Pop();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                undostackCrop.Push((Bitmap)bm.Clone());

                if (isDragging) { g.DrawImage(selection_image, selection_rect); }

                selection_pen.DashStyle = DashStyle.Dash;

                SaveState();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                pictureBox1.Invalidate();


                selection_bm = new Bitmap(selection_rect.Width, selection_rect.Height);
               
                Graphics ge = Graphics.FromImage(selection_bm);
                ge.DrawImage(bm, new Rectangle(0, 0, selection_rect.Width, selection_rect.Height), selection_rect, GraphicsUnit.Pixel);
                g.DrawRectangle(selection_pen, selection_rect);
                Clipboard.SetImage(selection_bm);

            }
        }
        //вырезать
        private void button3_Click(object sender, EventArgs e)
        {

            try
            {
                //не пустое прям-ник
                if (selection_rect != Rectangle.Empty)
                {
                    button1_Click(sender, e);

                    bm = undostackCrop.Pop();
                    g = Graphics.FromImage(bm);
                    pictureBox1.Image = bm;
                    isDragging = false;

                    g.SetClip(selection_rect);
                    g.Clear(Color.White);

                    SaveState();
                    undostackCrop.Push((Bitmap)bm.Clone());
                    pictureBox1.Invalidate();
                    selection_rect = Rectangle.Empty;//очищаем
                }
            }
            catch (Exception)
            {
                return;
            }
        }
        // вставить
        private void button4_Click(object sender, EventArgs e)
        {
            
            try
            {
               
                //Form1_KeyDown(sender, new KeyEventArgs(Keys.Control | Keys.V));
                if (selection_bm != null)
                {
                    Pen pen1 = new Pen(Color.Red, 1);
                    pen1.DashStyle = DashStyle.Dash;

                    bm = undostackCrop.Pop();
                    g = Graphics.FromImage(bm);
                    pictureBox1.Image = bm; //устанавливае

                    //рисуем 
                    selection_rect.Width = selection_bm.Width;
                    selection_rect.Height = selection_bm.Height;
                    selection_rect.X = 0;
                    selection_rect.Y = 0;
                    g.DrawImage(selection_bm, 0, 0);
                    g.DrawRectangle(pen1, 0, 0, selection_bm.Width, selection_bm.Height);
                    undostackCrop.Push((Bitmap)bm.Clone());

                    pictureBox1.Invalidate();                



                }
               Image image = Clipboard.GetImage();
                if (image != null)
                {
                    Pen pen1 = new Pen(Color.Red, 1);
                    pen1.DashStyle = DashStyle.Dash;
                    selection_bm = new Bitmap(image);
                    g = Graphics.FromImage(bm);
                    g.DrawImage(image, 0, 0, selection_bm.Width, selection_bm.Height);

                    selection_rect.Width = selection_bm.Width;
                    selection_rect.Height = selection_bm.Height;
                    selection_rect.X = 0;
                    selection_rect.Y = 0;
                    index = 7;
                    g.DrawRectangle(pen1, 0, 0, selection_bm.Width, selection_bm.Height);

                    undostackCrop.Push((Bitmap)bm.Clone());
                    pictureBox1.Image = bm;

                    pictureBox1.Invalidate();
                }

            }
            catch (Exception)
            {
                MessageBox.Show("хуйню сделала");
                return;
            }
        }

        private void SaveState()
        {
            //позволяет отменить изменения и вернуться к предыдущему состоянию
            undoStack.Push((Bitmap)bm.Clone());
        }
        void ChekCrop()
        {
            //стек не пустой и мы не в режиме перетаскивания
            if (undostackCrop.Count != 0 && !isDragging)
            {
                bm = undostackCrop.Pop();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                pictureBox1.Invalidate();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (undoStack.Count > 0)
                {
                    DialogResult result = MessageBox.Show("Вы хотите сохранить файл изображения?", "Сохранение файла", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        button7_Click(sender, e);
                        System.Windows.Forms.Application.Exit();
                        //e.Cancel = true;
                    }
                    else if (result == DialogResult.No)
                    {
                        System.Windows.Forms.Application.Exit();
                        //e.Cancel = false;
                    }
                    else
                    {

                        e.Cancel = true;
                    }
                }
                else
                {
                    e.Cancel = false;
                }
            }
        }

        //вперед на действие
        private void button5_Click(object sender, EventArgs e)
        {
            ChekCrop();

            if (redoStack.Count > 0)
            {
                undoStack.Push((Bitmap)bm.Clone());
                bm = redoStack.Pop();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                pictureBox1.Invalidate();
            }
        }
        //отмена действия
        private void button6_Click(object sender, EventArgs e)
        {
            ChekCrop();

            if (isDragging)
            {
                bm = undostackCrop.Pop();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                undostackCrop.Push((Bitmap)bm.Clone());
                g.DrawImage(selection_image, selection_rect);

                while (undoStack.Count > 0)
                {
                    undoStack.Pop();
                }
                selection_rect = Rectangle.Empty;
                isDragging = false;
            }

            if (undoStack.Count > 0)
            {
                redoStack.Push((Bitmap)bm.Clone());
                bm = undoStack.Pop();
                g = Graphics.FromImage(bm);
                pictureBox1.Image = bm;
                pictureBox1.Invalidate();
            }

        }
        // пипетка ind 8
        private void button14_Click(object sender, EventArgs e)
        {
            index = 8;
        }

       
    }
}
