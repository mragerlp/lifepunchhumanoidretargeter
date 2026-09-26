#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Sandbox;

namespace HumanoidRetargeter.Editor;

/// <summary>Shared look of the retargeter's windows: dark rounded cards on the window gray,
/// framed inputs, tinted pills and green accents.</summary>
internal static class RtStyle
{
	public const float ControlHeight = 24f;

	/// <summary>Height of every labelled settings field (inputs and dropdowns alike).</summary>
	public const float FieldHeight = 28f;
	public const float Radius = 4f;

	public static Color ButtonFill => Color.Lerp( Theme.ControlBackground.WithAlpha( 1f ), Color.White, .07f );
	public static Color InputEdge => Color.Lerp( Theme.ControlBackground.WithAlpha( 1f ), Color.White, .17f );

	/// <summary>Inputs sit on the cards' dark gray; a darker fill and a thin edge keep them readable.</summary>
	public static T Framed<T>( T input ) where T : Widget
	{
		input.SetStyles( $"background-color: {Theme.WindowBackground.Hex}; border: 1px solid {InputEdge.Hex}; border-radius: {Radius}px; padding-left: 4px;" );
		input.FixedHeight = ControlHeight;
		return input;
	}

	public static Label Muted( Label label, bool small = false )
	{
		label.SetStyles( small ? $"color: {Theme.TextLight.Hex}; font-size: 11px;" : $"color: {Theme.TextLight.Hex};" );
		return label;
	}

	/// <summary>A labelled settings row: a muted caption of the given width, vertically centered
	/// on the caller's field (see <see cref="Field{T}"/>).</summary>
	public static Layout FieldRow( Widget owner, Layout parent, string caption, float width, string tooltip = null )
	{
		var row = parent.AddRow();
		row.Spacing = 6;
		var label = row.Add( Muted( new Label( caption, owner ) { FixedWidth = width, FixedHeight = FieldHeight, ToolTip = tooltip } ) );
		label.Alignment = TextFlag.LeftCenter;
		return row;
	}

	/// <summary>A framed settings input or dropdown at the shared field height.</summary>
	public static T Field<T>( T input ) where T : Widget
	{
		Framed( input );
		input.FixedHeight = FieldHeight;
		return input;
	}

	public static Color Tone( ChipTone tone ) => tone switch
	{
		ChipTone.Green => Theme.Green,
		ChipTone.Amber => Theme.Yellow,
		_ => Theme.Red,
	};

	public static IconButton Icon( Widget parent, string icon, Action clicked, string tooltip, float size = ControlHeight )
		=> new( icon, clicked, parent )
		{
			FixedSize = size,
			IconSize = 15,
			ToolTip = tooltip,
			Background = ButtonFill,
			Foreground = Theme.Text,
			BackgroundActive = Theme.Green.WithAlpha( .2f ),
			ForegroundActive = Theme.Green,
		};

	/// <summary>Square icon toggle (skeleton, source overlay, ground).</summary>
	public static IconButton Toggle( Widget parent, string icon, bool on, Action<bool> toggled, string tooltip )
	{
		var button = Icon( parent, icon, null, tooltip );
		button.IsToggle = true;
		button.IsActive = on;
		button.OnToggled = toggled;
		return button;
	}

	public static Checkbox Check( Layout parent, string text, bool value, Action<bool> changed, string tooltip = null )
	{
		var box = parent.Add( new Checkbox( text ) { Value = value, ToolTip = tooltip } );
		box.Clicked = () => changed( box.Value );
		return box;
	}

	/// <summary>An engine-unit distance for the read-outs (inches).</summary>
	public static string Inches( float value ) => $"{MathF.Abs( value ):0.0} in";
}

/// <summary>A rounded dark-gray panel with an optional header row (icon, title, then controls).</summary>
internal class RtCard : Widget
{
	public RtCard( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 6;
	}

	public Layout Header( string icon, string title, string tooltip = null )
	{
		var row = Layout.AddRow();
		row.Spacing = 6;
		row.Add( new HeaderIcon( this, icon ) );
		var label = row.Add( new Label( title, this ) { ToolTip = tooltip, FixedHeight = RtStyle.ControlHeight } );
		label.SetStyles( "font-weight: 600;" );
		return row;
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		Paint.SetPen( Theme.ControlBackground.Lighten( .25f ), 1 );
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( LocalRect.Shrink( 1 ), 6 );
	}

	sealed class HeaderIcon : Widget
	{
		readonly string _icon;

		public HeaderIcon( Widget parent, string icon ) : base( parent )
		{
			_icon = icon;
			FixedSize = 18;
		}

		protected override void OnPaint()
		{
			Paint.SetPen( Theme.Green );
			Paint.DrawIcon( LocalRect, _icon, 16 );
		}
	}
}

/// <summary>A rounded tinted label: profile and confidence, ground verdicts, counts.</summary>
internal sealed class RtPill : Widget
{
	string _text = "";
	Color _color = Theme.TextLight;

	public RtPill( Widget parent, string text, Color color, string tooltip = null ) : base( parent )
	{
		FixedHeight = 18;
		ToolTip = tooltip;
		Set( text, color );
	}

	public void Set( string text, Color color, string tooltip = null )
	{
		text ??= "";
		if ( tooltip is not null )
			ToolTip = tooltip;
		_text = text;
		_color = color;
		FixedWidth = 6.6f * text.Length + 16;
		Visible = text.Length > 0;
		Update();
	}

	protected override void OnPaint()
	{
		if ( string.IsNullOrEmpty( _text ) )
			return;
		Paint.Antialiasing = true;
		Paint.ClearPen();
		Paint.SetBrush( _color.WithAlpha( 0.18f ) );
		Paint.DrawRect( LocalRect, LocalRect.Height * 0.5f );
		Paint.SetPen( _color );
		Paint.SetDefaultFont( 7, 600 );
		Paint.DrawText( LocalRect, _text );
	}
}

/// <summary>One line of text that is cut short with "…" to the width it is given. A plain label
/// asks for its full text width, so one long line (an error message) widened every row of the
/// clip list past the panel and cut their buttons off.</summary>
public sealed class RtElidedLabel : Widget
{
	string _text = "";
	readonly float _size;
	readonly int _weight;
	Color _color;

	public RtElidedLabel( Widget parent, float size = 9, int weight = 400 ) : base( parent )
	{
		_size = size;
		_weight = weight;
		_color = Theme.Text;
		MinimumWidth = 20;
		FixedHeight = size + 9;
	}

	public string Text
	{
		get => _text;
		set { _text = value ?? ""; Update(); }
	}

	public Color Color
	{
		get => _color;
		set { _color = value; Update(); }
	}

	protected override void OnPaint()
	{
		Paint.SetDefaultFont( _size, _weight );
		Paint.SetPen( _color );
		Paint.DrawText( LocalRect, Paint.GetElidedText( _text, Width, ElideMode.Right, TextFlag.LeftCenter ), TextFlag.LeftCenter );
	}
}

/// <summary>A small round light before the status text: blue while working, green when fine, red on errors.</summary>
internal sealed class RtStatusDot : Widget
{
	Color _color = Theme.TextLight;

	public RtStatusDot( Widget parent ) : base( parent )
	{
		FixedSize = 10;
	}

	public Color Color
	{
		get => _color;
		set
		{
			_color = value;
			Update();
		}
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		Paint.ClearPen();
		Paint.SetBrush( _color );
		Paint.DrawRect( LocalRect.Shrink( 1 ), 4 );
	}
}

/// <summary>The empty state of the clip list: a prompt and the add button. Accepts animation
/// files from the OS and from the asset browser.</summary>
internal sealed class RtDropZone : Widget
{
	readonly Action<IReadOnlyList<string>> _add;
	int _hover;

	public RtDropZone( Widget parent, Action<IReadOnlyList<string>> add, Action choose ) : base( parent )
	{
		_add = add;
		AcceptDrops = true;
		Layout = Layout.Column();
		Layout.Margin = 12;
		Layout.Spacing = 6;
		Layout.AddStretchCell();
		var title = Layout.Add( new Label( "Drop animation files here", this ) { Alignment = TextFlag.Center } );
		title.SetStyles( "font-weight: 600;" );
		Layout.Add( RtStyle.Muted( new Label( "FBX · BVH · GLB · glTF · VRM · ANM · AN5 · CBA — or right-click them in the Asset Browser", this )
			{ Alignment = TextFlag.Center, WordWrap = true }, small: true ) );
		var row = Layout.AddRow();
		row.AddStretchCell();
		row.Add( new Button.Primary( "Add Files…" ) { Icon = "add", Tint = Theme.Green, FixedHeight = 28, Clicked = choose } );
		row.AddStretchCell();
		Layout.AddStretchCell();
	}

	public override void OnDragHover( DragEvent e )
	{
		var valid = RtDrop.Paths( e.Data ).Count > 0;
		_hover = valid ? 1 : -1;
		if ( valid )
			e.Action = DropAction.Link;
		Update();
	}

	public override void OnDragDrop( DragEvent e )
	{
		_hover = 0;
		var paths = RtDrop.Paths( e.Data );
		if ( paths.Count > 0 )
		{
			e.Action = DropAction.Link;
			_add( paths );
		}
		Update();
	}

	public override void OnDragLeave()
	{
		_hover = 0;
		Update();
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		Paint.SetPen( _hover == 1 ? Theme.Green : _hover < 0 ? Theme.Red : Theme.ControlBackground.Lighten( .45f ), _hover == 0 ? 1 : 2 );
		Paint.SetBrush( _hover == 1 ? Theme.Green.WithAlpha( .06f ) : Theme.WindowBackground.WithAlpha( .5f ) );
		Paint.DrawRect( LocalRect.Shrink( 1 ), 6 );
	}
}

/// <summary>What can be dropped on the retargeter: animation files from disk or the asset browser.</summary>
internal static class RtDrop
{
	static readonly string[] Extensions = { ".fbx", ".bvh", ".glb", ".gltf", ".vrm", ".anm", ".an5", ".cba" };

	public static bool IsAnimationFile( string path )
		=> !string.IsNullOrEmpty( path ) && Extensions.Contains( System.IO.Path.GetExtension( path ).ToLowerInvariant() );

	public static IReadOnlyList<string> Paths( DragData data )
	{
		var paths = new List<string>();
		if ( data is null )
			return paths;
		try
		{
			if ( data.Files is { Length: > 0 } files )
				paths.AddRange( files.Where( IsAnimationFile ) );
			else if ( data.HasFileOrFolder && IsAnimationFile( data.FileOrFolder ) )
				paths.Add( data.FileOrFolder );
			if ( data.Assets is { Count: > 0 } assets )
			{
				foreach ( var asset in assets )
				{
					var path = asset?.AssetPath;
					if ( IsAnimationFile( path ) && AssetSystem.FindByPath( path )?.AbsolutePath is { } absolute )
						paths.Add( absolute );
				}
			}
		}
		catch ( Exception )
		{
			// A drag with nothing we understand.
		}
		return paths.Distinct( StringComparer.OrdinalIgnoreCase ).ToList();
	}
}

/// <summary>The secondary button (the Weapon Importer's): a fill one step lighter than the card,
/// a hairline border and a hover lighten.</summary>
internal sealed class RtButton : Widget
{
	string _text;
	readonly string _icon;

	public Action Clicked { get; set; }

	public RtButton( Widget parent, string text, string icon = null, Action clicked = null, string tooltip = null,
		float height = RtStyle.ControlHeight ) : base( parent )
	{
		_text = text ?? "";
		_icon = icon;
		Clicked = clicked;
		ToolTip = tooltip;
		FixedHeight = height;
		Cursor = CursorShape.Finger;
		MouseTracking = true;
		FocusMode = FocusMode.None;
		Measure();
	}

	public string Text
	{
		get => _text;
		set
		{
			_text = value ?? "";
			Measure();
			Update();
		}
	}

	const float PadX = 10f;
	const float IconSize = 16f;
	const float IconGap = 5f;

	void Measure() => FixedWidth = WidthFor( _text.Length == 0 ? 0 : 6.2f * _text.Length );

	float WidthFor( float textWidth )
	{
		if ( _text.Length == 0 )
			return string.IsNullOrEmpty( _icon ) ? PadX * 2 : RtStyle.ControlHeight;
		var icon = string.IsNullOrEmpty( _icon ) ? 0 : IconSize + IconGap;
		return MathF.Ceiling( PadX * 2 + icon + textWidth );
	}

	protected override void OnMouseEnter() => Update();
	protected override void OnMouseLeave() => Update();

	protected override void OnMousePress( MouseEvent e )
	{
		if ( e.LeftMouseButton )
			e.Accepted = true;
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );
		if ( !Enabled || !e.LeftMouseButton || !LocalRect.IsInside( e.LocalPosition ) )
			return;
		Clicked?.Invoke();
		e.Accepted = true;
	}

	protected override void OnPaint()
	{
		Paint.Antialiasing = true;
		var hover = Paint.HasMouseOver && Enabled;
		var edge = Color.Lerp( Theme.ControlBackground.WithAlpha( 1f ), Color.White, hover ? .25f : .15f );
		Paint.SetPen( edge, 1 );
		Paint.SetBrush( hover ? Color.Lerp( RtStyle.ButtonFill, Color.White, .06f ) : RtStyle.ButtonFill );
		Paint.DrawRect( LocalRect.Shrink( .5f ), RtStyle.Radius );

		Paint.SetPen( Enabled ? Theme.Text : Theme.TextDisabled );
		if ( _text.Length == 0 )
		{
			if ( !string.IsNullOrEmpty( _icon ) )
				Paint.DrawIcon( LocalRect, _icon, 15, TextFlag.Center );
			return;
		}
		Paint.SetDefaultFont( 8 );
		var textWidth = Paint.MeasureText( _text ).x;
		var wanted = WidthFor( textWidth );
		if ( MathF.Abs( wanted - FixedWidth ) > 0.5f )
			FixedWidth = wanted;
		var hasIcon = !string.IsNullOrEmpty( _icon );
		var group = textWidth + (hasIcon ? IconSize + IconGap : 0);
		var x = LocalRect.Left + MathF.Max( PadX, (LocalRect.Width - group) * 0.5f );
		if ( hasIcon )
		{
			Paint.DrawIcon( new Rect( x, LocalRect.Top, IconSize, LocalRect.Height ), _icon, 15, TextFlag.Center );
			x += IconSize + IconGap;
		}
		Paint.DrawText( new Rect( x, LocalRect.Top, LocalRect.Right - x, LocalRect.Height ), _text, TextFlag.LeftCenter );
	}
}

/// <summary>Small uppercase caption with a hairline, separating groups inside a card.</summary>
internal sealed class RtSection : Widget
{
	readonly string _text;

	public RtSection( Widget parent, string text ) : base( parent )
	{
		_text = text.ToUpperInvariant();
		FixedHeight = 18;
	}

	protected override void OnPaint()
	{
		Paint.SetDefaultFont( 7, 600 );
		Paint.SetPen( Theme.TextLight );
		var size = Paint.MeasureText( _text );
		Paint.DrawText( new Rect( 0, 0, size.x + 2, Height ), _text, TextFlag.LeftCenter );
		Paint.SetPen( Theme.ControlBackground.Lighten( .45f ), 1 );
		Paint.DrawLine( new Vector2( size.x + 10, Height * .5f ), new Vector2( Width, Height * .5f ) );
	}
}
