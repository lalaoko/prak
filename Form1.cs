
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using MiniCad.Models;
using MiniCad.Controls;
using MiniCad.Enums;

namespace MiniCad;

public partial class Form1 : Form
{
    private readonly List<Shape> _shapes = new();

    private Shape? _selectedShape;
    private Point _dragStartPoint;
    private bool _isDragging;


    private bool _isCreatingShape;
    private Point _creationStartPoint;



    private ToolMode _currentMode = ToolMode.Select;

    private Color _strokeColor = Color.Black;
    private Color _fillColor = Color.LightSkyBlue;
    private float _strokeWidth = 2f;

    private CanvasPanel canvas = null!;
    private PropertyGrid propertyGrid = null!;
    private ToolStripStatusLabel lblMode = null!;
    private ToolStripStatusLabel lblCoords = null!;
    private ToolStripStatusLabel lblCount = null!;

    public Form1()
    {
        InitializeComponent();
        BuildInterface();

        KeyPreview = true;
        KeyDown += Form1_KeyDown;
    }



    private void BuildInterface()
    {
        Text = "Mini-CAD — графический редактор";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1100, 700);
        MinimumSize = new Size(800, 500);

        var toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = true
        };

        AddToolButton(toolStrip, "Выделение", ToolMode.Select);
        AddToolButton(toolStrip, "Прямоугольник", ToolMode.Rectangle);
        AddToolButton(toolStrip, "Эллипс", ToolMode.Ellipse);
        AddToolButton(toolStrip, "Ромб", ToolMode.Rhombus);
        AddToolButton(toolStrip, "Текст", ToolMode.Text);
        AddToolButton(toolStrip, "Линия", ToolMode.Line);

        toolStrip.Items.Add(new ToolStripSeparator());

        var strokeButton = new ToolStripButton("Контур");
        strokeButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialog
            {
                Color = _strokeColor
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _strokeColor = dialog.Color;

                if (_selectedShape != null)
                    _selectedShape.StrokeColor = _strokeColor;

                canvas.Invalidate();
                propertyGrid.Refresh();
            }
        };

        toolStrip.Items.Add(strokeButton);

        var fillButton = new ToolStripButton("Заливка");
        fillButton.Click += (_, _) =>
        {
            using var dialog = new ColorDialog
            {
                Color = _fillColor
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _fillColor = dialog.Color;

                if (_selectedShape != null)
                    _selectedShape.FillColor = _fillColor;

                canvas.Invalidate();
                propertyGrid.Refresh();
            }
        };

        toolStrip.Items.Add(fillButton);

        toolStrip.Items.Add(new ToolStripLabel("Толщина:"));

        var thicknessBox = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        thicknessBox.Items.AddRange(new object[]
        {
            "1", "2", "3", "5", "8"
        });

        thicknessBox.SelectedItem = "2";

        thicknessBox.SelectedIndexChanged += (_, _) =>
        {
            if (float.TryParse(
                thicknessBox.SelectedItem?.ToString(),
                out float width))
            {
                _strokeWidth = width;

                if (_selectedShape != null)
                    _selectedShape.StrokeWidth = width;

                canvas.Invalidate();
                propertyGrid.Refresh();
            }
        };

        toolStrip.Items.Add(thicknessBox);

        toolStrip.Items.Add(new ToolStripSeparator());

        var clearButton = new ToolStripButton("Очистить");
        clearButton.Click += (_, _) =>
        {
            if (MessageBox.Show(
                "Удалить все фигуры с холста?",
                "Очистка холста",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _shapes.Clear();
                _selectedShape = null;
                propertyGrid.SelectedObject = null;
                canvas.Invalidate();
                UpdateCount();
            }
        };

        toolStrip.Items.Add(clearButton);

        var saveButton = new ToolStripButton("Сохранить JSON");
        saveButton.Click += SaveJson;
        toolStrip.Items.Add(saveButton);

        var loadButton = new ToolStripButton("Загрузить JSON");
        loadButton.Click += LoadJson;
        toolStrip.Items.Add(loadButton);

        var exportButton = new ToolStripButton("Экспорт PNG");
        exportButton.Click += ExportPng;
        toolStrip.Items.Add(exportButton);

        propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Right,
            Width = 250,
            ToolbarVisible = true,
            HelpVisible = true
        };

        propertyGrid.PropertyValueChanged += (_, _) =>
        {
            canvas.Invalidate();
        };

        canvas = new CanvasPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        canvas.Paint += Canvas_Paint;
        canvas.MouseDown += Canvas_MouseDown;
        canvas.MouseMove += Canvas_MouseMove;
        canvas.MouseUp += Canvas_MouseUp;

        var statusStrip = new StatusStrip();

        lblMode = new ToolStripStatusLabel("Инструмент: Выделение");
        lblCoords = new ToolStripStatusLabel("X: 0, Y: 0");
        lblCount = new ToolStripStatusLabel("Фигур: 0");

        statusStrip.Items.Add(lblMode);
        statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        statusStrip.Items.Add(lblCoords);
        statusStrip.Items.Add(new ToolStripStatusLabel("|"));
        statusStrip.Items.Add(lblCount);

        Controls.Add(canvas);
        Controls.Add(propertyGrid);
        Controls.Add(toolStrip);
        Controls.Add(statusStrip);
    }

    private void AddToolButton(
        ToolStrip strip,
        string title,
        ToolMode mode)
    {
        var button = new ToolStripButton(title)
        {
            CheckOnClick = false
        };

        button.Click += (_, _) =>
        {
            _currentMode = mode;
            lblMode.Text = $"Инструмент: {title}";
        };

        strip.Items.Add(button);
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        foreach (Shape shape in _shapes)
        {
            shape.Draw(e.Graphics);
        }
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        if (_currentMode == ToolMode.Select)
        {
            if (_selectedShape != null)
                _selectedShape.IsSelected = false;

            _selectedShape = _shapes.LastOrDefault(
                shape => shape.ContainsPoint(e.Location));

            if (_selectedShape != null)
            {
                _selectedShape.IsSelected = true;
                _isDragging = true;
                _dragStartPoint = e.Location;
                propertyGrid.SelectedObject = _selectedShape;
            }
            else
            {
                propertyGrid.SelectedObject = null;
            }

            canvas.Invalidate();
            return;
        }

        Shape? newShape = _currentMode switch
        {
            ToolMode.Rectangle => new RectangleShape(),
            ToolMode.Ellipse => new EllipseShape(),
            ToolMode.Rhombus => new RhombusShape(),
            ToolMode.Text => new TextBlockShape(),
            ToolMode.Line => new LineShape(),
            _ => null
        };

        if (newShape == null)
            return;

        if (_selectedShape != null)
            _selectedShape.IsSelected = false;

        _creationStartPoint = e.Location;
        _isCreatingShape = true;

        newShape.X = e.X;
        newShape.Y = e.Y;
        newShape.Width = 10;
        newShape.Height = 10;
        newShape.StrokeColor = _strokeColor;
        newShape.FillColor = _fillColor;
        newShape.StrokeWidth = _strokeWidth;

        _shapes.Add(newShape);
        _selectedShape = newShape;
        _selectedShape.IsSelected = true;

        propertyGrid.SelectedObject = _selectedShape;

        canvas.Invalidate();
        UpdateCount();
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        lblCoords.Text = $"X: {e.X}, Y: {e.Y}";

        // Создание фигуры перетаскиванием мыши
        if (_isCreatingShape && _selectedShape != null)
        {
            int left = Math.Min(_creationStartPoint.X, e.X);
            int top = Math.Min(_creationStartPoint.Y, e.Y);

            int width = Math.Max(10, Math.Abs(e.X - _creationStartPoint.X));
            int height = Math.Max(10, Math.Abs(e.Y - _creationStartPoint.Y));

            _selectedShape.X = left;
            _selectedShape.Y = top;
            _selectedShape.Width = width;
            _selectedShape.Height = height;

            propertyGrid.Refresh();
            canvas.Invalidate();
            return;
        }

        // Перемещение уже созданной фигуры
        if (_isDragging && _selectedShape != null)
        {
            int dx = e.X - _dragStartPoint.X;
            int dy = e.Y - _dragStartPoint.Y;

            _selectedShape.Move(dx, dy);
            _dragStartPoint = e.Location;

            propertyGrid.Refresh();
            canvas.Invalidate();
        }
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
        _isCreatingShape = false;
    }

    private void UpdateCount()
    {
        lblCount.Text = $"Фигур: {_shapes.Count}";
    }

    private void SaveJson(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON схема (*.json)|*.json",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(
                _shapes, options);

            System.IO.File.WriteAllText(dialog.FileName, json);

            MessageBox.Show("Схема сохранена.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
    }

    private void LoadJson(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON схема (*.json)|*.json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            string json = System.IO.File.ReadAllText(dialog.FileName);

            var loaded =
                System.Text.Json.JsonSerializer.Deserialize<List<Shape>>(
                    json);

            if (loaded == null)
                return;

            _shapes.Clear();
            foreach (Shape shape in loaded)
            {
                shape.IsSelected = false;
                _shapes.Add(shape);
            }

            _selectedShape = null;
            propertyGrid.SelectedObject = null;

            canvas.Invalidate();
            UpdateCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
    }

    private void ExportPng(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG изображение (*.png)|*.png",
            DefaultExt = "png"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            using var bitmap = new Bitmap(
                canvas.ClientSize.Width,
                canvas.ClientSize.Height);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (Shape shape in _shapes)
                {
                    shape.Draw(g);
                }
            }

            bitmap.Save(
                dialog.FileName,
                System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}");
        }
    }

    private void Form1_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && _selectedShape != null)
        {
            _shapes.Remove(_selectedShape);
            _selectedShape = null;

            propertyGrid.SelectedObject = null;
            canvas.Invalidate();
            UpdateCount();

            e.Handled = true;
        }

        if (e.KeyCode == Keys.Escape)
        {
            if (_selectedShape != null)
            {
                _selectedShape.IsSelected = false;
                _selectedShape = null;
                propertyGrid.SelectedObject = null;
                canvas.Invalidate();
            }

            _isDragging = false;
            _isCreatingShape = false;
        }
    }
}


