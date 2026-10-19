using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NubeBar;

// Lo que suena, visto desde la isla: una mini carátula y unas barritas de
// ecualizador, como hace el iPhone cuando tienes música de fondo.
public partial class MainWindow
{
    EstadoMusica? _musica;
    DateTime _musicaUltimo;
    bool _musicaOcupada;

    readonly List<Border> _barras = new();
    Storyboard? _baile;

    // Se consulta cada tick, pero la sesión de medios es cara: cada 2 s basta
    async void RefrescarMusica()
    {
        if (_musicaOcupada || DateTime.Now - _musicaUltimo < TimeSpan.FromSeconds(2)) return;
        _musicaOcupada = true;
        _musicaUltimo = DateTime.Now;
        try
        {
            var antes = _musica;
            _musica = await Musica.Leer();
            if (antes?.Sonando != _musica?.Sonando) MoverBarras(_musica?.Sonando == true);
        }
        finally { _musicaOcupada = false; }
    }

    // ---------- Módulos de la isla ----------
    UIElement ModuloCaratula()
    {
        var marco = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        var img = new Image { Stretch = Stretch.UniformToFill };
        marco.Child = img;

        _actualizadores.Add(() =>
        {
            bool hay = _musica != null && Musica.Caratula != null;
            if (hay && !ReferenceEquals(img.Source, Musica.Caratula))
            {
                img.Source = Musica.Caratula;
                img.BeginAnimation(OpacityProperty, Kf(0, 1, 260));
            }
            var quiero = hay ? Visibility.Visible : Visibility.Collapsed;
            if (marco.Visibility != quiero) marco.Visibility = quiero;
        });
        return marco;
    }

    const int CuantasBarras = 4;

    UIElement ModuloBarras()
    {
        var pila = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 16,
            Margin = new Thickness(4, 0, 4, 0),
            Visibility = Visibility.Collapsed
        };

        _barras.Clear();
        for (int i = 0; i < CuantasBarras; i++)
        {
            var b = new Border
            {
                Width = 3,
                Height = 5,
                CornerRadius = new CornerRadius(1.5),
                Background = _acento,
                Margin = new Thickness(1.5, 0, 1.5, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _barras.Add(b);
            pila.Children.Add(b);
        }

        _actualizadores.Add(() =>
        {
            var quiero = _musica != null ? Visibility.Visible : Visibility.Collapsed;
            if (pila.Visibility != quiero)
            {
                pila.Visibility = quiero;
                MoverBarras(_musica?.Sonando == true);
            }
        });
        return pila;
    }

    // Cada barra sube y baja a su ritmo; en pausa se quedan quietas y bajitas
    void MoverBarras(bool sonando)
    {
        if (_barras.Count == 0) return;

        _baile?.Stop(this);
        _baile = null;

        if (!sonando)
        {
            foreach (var b in _barras)
            {
                b.BeginAnimation(HeightProperty, null);
                b.Height = 4;
            }
            return;
        }

        var guion = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        double[] altos = { 14, 9, 16, 11 };
        double[] tiempos = { 520, 380, 610, 450 };

        for (int i = 0; i < _barras.Count; i++)
        {
            var anim = new DoubleAnimationUsingKeyFrames { Duration = Ms(tiempos[i % tiempos.Length]) };
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromPercent(0)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(altos[i % altos.Length], KeyTime.FromPercent(0.5))
            { EasingFunction = Suave });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromPercent(1)) { EasingFunction = Suave });
            anim.AutoReverse = false;

            Storyboard.SetTarget(anim, _barras[i]);
            Storyboard.SetTargetProperty(anim, new PropertyPath(HeightProperty));
            guion.Children.Add(anim);
        }
        _baile = guion;
        guion.Begin(this, true);
    }
}
