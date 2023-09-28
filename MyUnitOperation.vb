Imports DWSIM.Interfaces.Enums
Imports SkiaSharp.Views.Desktop.Extensions
Imports System.Windows.Forms
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.UnitOperations.Streams
Imports XFlowsheet.Implementation.DefaultImplementations

Public Class MyUnitOperation

    Inherits UnitOperations.UnitOpBaseClass

    Implements Interfaces.IExternalUnitOperation

#Region "Unit Operation Information"

    Private Property UOName As String = "Humidifier"
    Private Property UODescription As String = "Humidifier Unit Operation"

    Public ReadOnly Property Prefix As String Implements Interfaces.IExternalUnitOperation.Prefix
        Get
            Return "MYUO-"
        End Get
    End Property

    Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.MixersSplitters

    Public Overrides Function GetDisplayName() As String
        Return UOName
    End Function

    Public Overrides Function GetDisplayDescription() As String
        Return UODescription
    End Function

    'tells DWSIM that this Unit Operation is or isn't compatible with mobile versions
    Public Overrides ReadOnly Property MobileCompatible As Boolean = False

#End Region

#Region "Initialization and Cloning Support"

    Public Sub New(ByVal Name As String, ByVal Description As String)

        MyBase.CreateNew()
        Me.ComponentName = Name
        Me.ComponentDescription = Description

    End Sub

    Public Sub New()

        MyBase.New()

    End Sub

    'returns a new instance of this unit operation
    Public Function ReturnInstance(typename As String) As Object Implements Interfaces.IExternalUnitOperation.ReturnInstance

        Return New MyUnitOperation()

    End Function

    'returns a new instance of unit operation, using XML cloning
    Public Overrides Function CloneXML() As Object

        Dim objdata = XMLSerializer.XMLSerializer.Serialize(Me)
        Dim newhumidifier As New MyUnitOperation()
        newhumidifier.LoadData(objdata)

        Return newhumidifier

    End Function

    'returns a new instance of humidifer, using JSON cloning
    Public Overrides Function CloneJSON() As Object

        Dim jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(Me)
        Dim newhumidifier = Newtonsoft.Json.JsonConvert.DeserializeObject(Of MyUnitOperation)(jsonstring)

        Return newhumidifier

    End Function

#End Region

#Region "Calculation Routine"
    Public Overrides Sub Calculate(Optional args As Object = Nothing)

        Dim gas_stream As MaterialStream = GetInletMaterialStream(0)

        Dim water_stream As MaterialStream = GetInletMaterialStream(1)

        Dim outletstream = GetOutletMaterialStream(0)

        Dim energystream As EnergyStream = GetOutletEnergyStream(1)

        If gas_stream Is Nothing Then
            Throw New Exception("No stream connected to inlet gas port")
        End If

        If water_stream Is Nothing Then
            Throw New Exception("No stream connected to inlet water port")
        End If

        If outletstream Is Nothing Then
            Throw New Exception("No stream connected to outlet gas port")
        End If

        If energystream Is Nothing Then
            Throw New Exception("No stream connected to outlet energy port")
        End If

        'check if water stream is really made of liquid water only.

        If Not water_stream.GetPhase("Overall").Compounds.Keys.Contains("Water") Then
            Throw New Exception("This Unit Operation needs Water in the list of added compounds.")
        End If

        'get water compound amount in liquid phase.

        Dim liquidphase_water = water_stream.GetPhase("Liquid1")

        Dim water = liquidphase_water.Compounds("Water")

        If liquidphase_water.Properties.molarfraction < 0.9999 And water.MoleFraction < 0.9999 Then
            Throw New Exception("The inlet water stream must be 100% liquid water.")
        End If

        Dim mixedstream As MaterialStream = gas_stream.Clone()

        mixedstream = mixedstream.Add(water_stream.Clone())

        Dim Pgas = gas_stream.GetPressure() 'Pa
        Dim Pwater = water_stream.GetPressure() 'Pa

        'set outlet stream pressure as (Pg + Pw)/2

        mixedstream.SetPressure((Pgas + Pwater) / 2)

        Dim Hg = gas_stream.GetMassEnthalpy() 'kJ/kg
        Dim Hw = water_stream.GetMassEnthalpy() 'kJ/kg

        Dim Wg = gas_stream.GetMassFlow() 'kg/s
        Dim Ww = water_stream.GetMassFlow() 'kg/s

        'isothermic mode means outlet temperature = gas temperature

        Dim Tg = gas_stream.GetTemperature() 'K

        mixedstream.SetTemperature(Tg)
        mixedstream.SetFlashSpec("PT") 'Pressure/Temperature

        'calculate the stream to get its enthalpy and close the energy balance.

        Me.PropertyPackage.CurrentMaterialStream = mixedstream
        mixedstream.Calculate()

        Dim Wo = mixedstream.GetMassFlow() 'kg/s
        Dim Ho = mixedstream.GetMassEnthalpy() 'kJ/kg

        Dim Eb = (Wg * Hg + Ww * Hw) - Wo * Ho 'kJ/s = KW

        energystream.EnergyFlow = Eb 'kW

        'copy the properties from mixedstream

        outletstream.Assign(mixedstream)

    End Sub

#End Region

#Region "Automatic Unit Operation Implementation"

    Public Property InputParameters As Dictionary(Of String, Parameter) = New Dictionary(Of String, Parameter)()

    Public Property OutputParameters As Dictionary(Of String, Parameter) = New Dictionary(Of String, Parameter)()

    Public Property ConnectionPorts As List(Of ConnectionPort) = New List(Of ConnectionPort)()

    Public Overrides Property ComponentName As String = UOName

    Public Overrides Property ComponentDescription As String = UODescription

    Private ReadOnly Property IExternalUnitOperation_Name As String Implements Interfaces.IExternalUnitOperation.Name
        Get
            Return UOName
        End Get
    End Property

    Public ReadOnly Property Description As String Implements Interfaces.IExternalUnitOperation.Description
        Get
            Return UODescription
        End Get
    End Property

#End Region

#Region "Automatic Drawing Support"

    Public Overrides Function GetIconBitmap() As Object
        Return My.Resources.icon
    End Function

    Private Image As SkiaSharp.SKImage

    'this function draws the object on the flowsheet
    Public Sub Draw(g As Object) Implements Interfaces.IExternalUnitOperation.Draw

        'get the canvas object
        Dim canvas = DirectCast(g, SkiaSharp.SKCanvas)

        'load the icon image on memory
        If Image Is Nothing Then

            Using bitmap = My.Resources.icon.ToSKBitmap()
                Image = SkiaSharp.SKImage.FromBitmap(bitmap)
            End Using

        End If

        Dim x = Me.GraphicObject.X
        Dim y = Me.GraphicObject.Y
        Dim w = Me.GraphicObject.Width
        Dim h = Me.GraphicObject.Height

        'draw the image into the flowsheet inside the object's reserved rectangle area
        Using p As New SkiaSharp.SKPaint With {.FilterQuality = SkiaSharp.SKFilterQuality.High}
            canvas.DrawImage(Image, New SkiaSharp.SKRect(GraphicObject.X, GraphicObject.Y, GraphicObject.X + GraphicObject.Width, GraphicObject.Y + GraphicObject.Height), p)
        End Using

    End Sub

    'this function creates the connection ports in the flowsheet object
    Public Sub CreateConnectors() Implements Interfaces.IExternalUnitOperation.CreateConnectors

        If GraphicObject.InputConnectors.Count = 0 Then

            Dim port1 As New Drawing.SkiaSharp.GraphicObjects.ConnectionPoint()

            port1.IsEnergyConnector = False
            port1.Type = Interfaces.Enums.GraphicObjects.ConType.ConIn
            port1.Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X, GraphicObject.Y + GraphicObject.Height / 3)
            port1.ConnectorName = "Gas Stream Inlet Port"

            GraphicObject.InputConnectors.Add(port1)

            Dim port2 As New Drawing.SkiaSharp.GraphicObjects.ConnectionPoint()

            port2.IsEnergyConnector = False
            port2.Type = Interfaces.Enums.GraphicObjects.ConType.ConIn
            port2.Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X, GraphicObject.Y + GraphicObject.Height * 2 / 3)
            port2.ConnectorName = "Water Stream Inlet Port"

            GraphicObject.InputConnectors.Add(port2)

        Else

            GraphicObject.InputConnectors(0).Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X, GraphicObject.Y + GraphicObject.Height / 3)

            GraphicObject.InputConnectors(1).Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X, GraphicObject.Y + GraphicObject.Height * 2 / 3)

        End If

        If GraphicObject.OutputConnectors.Count = 0 Then

            Dim port3 As New Drawing.SkiaSharp.GraphicObjects.ConnectionPoint()

            port3.IsEnergyConnector = False
            port3.Type = Interfaces.Enums.GraphicObjects.ConType.ConOut
            port3.Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + GraphicObject.Width, GraphicObject.Y + GraphicObject.Height / 2)
            port3.ConnectorName = "Mixed Stream Outlet Port"

            GraphicObject.OutputConnectors.Add(port3)

            Dim port4 As New Drawing.SkiaSharp.GraphicObjects.ConnectionPoint()
            port4.IsEnergyConnector = True
            port4.Type = Interfaces.Enums.GraphicObjects.ConType.ConEn
            port4.Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + GraphicObject.Width / 2, GraphicObject.Y + GraphicObject.Height)
            port4.ConnectorName = "Energy Stream Outlet Port"

            GraphicObject.OutputConnectors.Add(port4)

        Else

            GraphicObject.OutputConnectors(0).Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + GraphicObject.Width, GraphicObject.Y + GraphicObject.Height / 2)
            GraphicObject.OutputConnectors(1).Position = New DWSIM.DrawingTools.Point.Point(GraphicObject.X + GraphicObject.Width / 2, GraphicObject.Y + GraphicObject.Height)

        End If

        GraphicObject.EnergyConnector.Active = False

    End Sub

#End Region

#Region "Classic UI and Cross-Platform UI Editor Support"

    'display the editor on the classic user interface
    Public Overrides Sub DisplayEditForm()

        'If editwindow Is Nothing Then

        '    editwindow = New Editor() With {.HObject = Me}

        'Else

        '    If editwindow.IsDisposed Then
        '        editwindow = New Editor() With {.HObject = Me}
        '    End If

        'End If

        'FlowSheet.DisplayForm(editwindow)

    End Sub

    'this updates the editor window on classic ui
    Public Overrides Sub UpdateEditForm()

        'If editwindow IsNot Nothing Then

        '    If editwindow.InvokeRequired Then

        '        editwindow.Invoke(Sub()
        '                              editwindow?.UpdateInfo()
        '                          End Sub)

        '    Else

        '        editwindow?.UpdateInfo()

        '    End If

        'End If

    End Sub

    'this closes the editor on classic ui
    Public Overrides Sub CloseEditForm()

        'editwindow?.Close()

    End Sub

    'returns the editing form
    Public Overrides Function GetEditingForm() As Form

        Return Nothing

    End Function

    'this function display the properties on the cross-platform user interface
    Public Sub PopulateEditorPanel(container As Object) Implements Interfaces.IExternalUnitOperation.PopulateEditorPanel

        'using extension methods from DWSIM.ExtensionMethods.Eto (DWISM.UI.Shared namespace)

        Dim propertiespanel = DirectCast(container, Eto.Forms.DynamicLayout)

    End Sub

#End Region

End Class