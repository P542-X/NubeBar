using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace NubeBar;

// La barra de desplazamiento de NubeBar.
//
// ---------- Por qué ----------
//
// Cuando un panel no cabe —los laterales, la red con quince wifis, el centro de
// notificaciones— sale su barra de desplazamiento, y salía la de Windows: gris claro, con
// flechas arriba y abajo y 17 px de ancho, pegada a una tarjeta negra y redondeada. Lo
// contó el usuario: «sale la barra de subir y bajar de Windows, tendríamos que tener una
// personalizada».
//
// ---------- Cómo es ----------
//
// Sin flechas y sin carril visible: una pastilla redondeada del color del texto, a medio
// tono, que engorda cuando el ratón pasa por la barra y se pinta del acento mientras la
// arrastras. El carril sigue ahí aunque no se vea: pulsar en él avanza una página, como en
// la de Windows.
//
// Se pone como estilo IMPLÍCITO de la aplicación (ver `AplicarTema`), así que llega a todos
// los scrolls —paneles, laterales, ventanas flotantes, Ajustes— sin tocar ninguno, y se
// rehace con el tema.
//
// Va en XAML y no a base de `FrameworkElementFactory`: una plantilla con disparadores
// escrita a mano en código es el triple de larga y bastante menos legible.
static class BarraDeScroll
{
    public const double Grosor = 10;

    public static Style Estilo(Color tinta, Color acento)
    {
        string t = Hex(tinta), a = Hex(acento);
        string xaml = $$$"""
<Style TargetType="ScrollBar"
       xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style.Resources>
    <!-- El carril: invisible, pero pulsar en él sigue avanzando una página -->
    <Style x:Key="Carril" TargetType="RepeatButton">
      <Setter Property="Focusable" Value="False"/>
      <Setter Property="IsTabStop" Value="False"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="RepeatButton">
            <Border Background="Transparent"/>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <!-- La pastilla: fina en reposo, más gruesa con el ratón en la barra, del acento al arrastrar -->
    <Style x:Key="Pastilla" TargetType="Thumb">
      <Setter Property="Focusable" Value="False"/>
      <Setter Property="IsTabStop" Value="False"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Thumb">
            <Border x:Name="Forma" Background="{{{t}}}" Opacity="0.5" CornerRadius="3" Margin="3,2"/>
            <ControlTemplate.Triggers>
              <DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=ScrollBar}}" Value="True">
                <Setter TargetName="Forma" Property="Margin" Value="1.5,2"/>
                <Setter TargetName="Forma" Property="Opacity" Value="0.65"/>
                <Setter TargetName="Forma" Property="CornerRadius" Value="4"/>
              </DataTrigger>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Forma" Property="Opacity" Value="0.85"/>
              </Trigger>
              <Trigger Property="IsDragging" Value="True">
                <Setter TargetName="Forma" Property="Background" Value="{{{a}}}"/>
                <Setter TargetName="Forma" Property="Opacity" Value="1"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Style.Resources>

  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="Focusable" Value="False"/>
  <Setter Property="Stylus.IsFlicksEnabled" Value="False"/>
  <Setter Property="MinWidth" Value="0"/>
  <Setter Property="MinHeight" Value="0"/>
  <Setter Property="Width" Value="{{{Grosor}}}"/>
  <Setter Property="Height" Value="NaN"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="ScrollBar">
        <Grid Background="Transparent">
          <Track x:Name="PART_Track" IsDirectionReversed="True">
            <Track.DecreaseRepeatButton>
              <RepeatButton Style="{StaticResource Carril}" Command="ScrollBar.PageUpCommand"/>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Style="{StaticResource Carril}" Command="ScrollBar.PageDownCommand"/>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Style="{StaticResource Pastilla}"/>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>

  <Style.Triggers>
    <Trigger Property="Orientation" Value="Horizontal">
      <Setter Property="Width" Value="NaN"/>
      <Setter Property="Height" Value="{{{Grosor}}}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ScrollBar">
            <Grid Background="Transparent">
              <Track x:Name="PART_Track" IsDirectionReversed="False">
                <Track.DecreaseRepeatButton>
                  <RepeatButton Style="{StaticResource Carril}" Command="ScrollBar.PageLeftCommand"/>
                </Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton>
                  <RepeatButton Style="{StaticResource Carril}" Command="ScrollBar.PageRightCommand"/>
                </Track.IncreaseRepeatButton>
                <Track.Thumb>
                  <Thumb Style="{StaticResource Pastilla}"/>
                </Track.Thumb>
              </Track>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Trigger>
  </Style.Triggers>
</Style>
""";
        return (Style)XamlReader.Parse(xaml);
    }

    static string Hex(Color c) => $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
}
