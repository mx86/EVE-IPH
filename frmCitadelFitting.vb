﻿Imports System.Data.SQLite

Public Class frmCitadelFitting

    Declare Function SendMessage Lib "User32" Alias "SendMessageA" (ByVal hWnd As Integer, ByVal wMsg As Integer, ByVal wParam As Integer, ByVal lParam As Integer) As Integer
    Const WM_NCLBUTTONDOWN As Integer = &HA1
    Const HTCAPTION As Integer = 2

    Private SlotPictureBoxList As New List(Of PictureBox)
    Private FirstLoad As Boolean
    Private UpdateChecks As Boolean

    ' Public settings after intialized and returned for setting in the facilities
    Public CitadelName As String = ""
    Private SelectedStructureView As FacilityView ' To help determine where we save citadels, etc. 
    Private SelectedCharacterID As Long
    Private SelectedFacilityProductionType As ProductionType

    Private Attributes As New EVEAttributes
    ' Stores all the stats for the selected citadel
    Private UpwellStructureStats As New CitadelAttributes
    ' Save the selected Upwell Structure so we don't need to look it up
    Private SelectedUpwellStructure As UpwellStructureDBData

    Private POSFuelPricesUpdated As Boolean

    Private Const UsesMissilesEffectID As Integer = 101

    Private StructureDBDataList As New List(Of UpwellStructureDBData) ' For storing all the types of citadel structures

    Private HighSlotBaseX As Integer
    Private HighSlotBaseWidth As Integer
    Private HighSlotSpacing As Integer
    Private ServiceSlotBaseX As Integer
    Private ServiceSlotBaseWidth As Integer
    Private ServiceSlotSpacing As Integer

    Private SecurityCheckBoxes As List(Of CheckBox)

    ' Used to look up modules and rigs to go into what slot
    Private Enum SlotSizes
        LowSlot = 11
        MediumSlot = 13
        HighSlot = 12
    End Enum

    Public Structure StructureModule
        Dim typeID As Integer
        Dim moduleType As String
    End Structure

    Private Structure UpwellStructureDBData
        Dim Name As String
        Dim TypeID As Integer
        Dim GroupID As Integer
    End Structure

    ' For saving and updating the selected upwell structure
    Private Structure CitadelAttributes
        Dim CPU As Double
        Dim MaxCPU As Double
        Dim PG As Double
        Dim MaxPG As Double
        Dim Calibration As Double
        Dim MaxCalibration As Double
        Dim Capacitor As Double
        Dim MaxCapacitor As Double
        Dim CapacitorRechargeRate As Double
        Dim BaseCapRechargeRate As Double
        Dim ServiceModuleFuelBPH As Integer ' blocks per hour

        Dim LauncherSlots As Integer

    End Structure

    Public Sub New(ByVal InitName As String, ByVal CharacterID As Long, ByVal FacilityType As ProductionType,
                   ByVal FacilityLocation As FacilityView, ByVal FacilitySystemSecurity As Double)
        FirstLoad = True

        ' This call is required by the designer.
        InitializeComponent()

        ' Put all the slot images into an array
        With SlotPictureBoxList
            .Add(HighSlot1)
            .Add(HighSlot2)
            .Add(HighSlot3)
            .Add(HighSlot4)
            .Add(HighSlot5)
            .Add(HighSlot6)
            .Add(HighSlot7)
            .Add(HighSlot8)

            .Add(MidSlot1)
            .Add(MidSlot2)
            .Add(MidSlot3)
            .Add(MidSlot4)
            .Add(MidSlot5)
            .Add(MidSlot6)
            .Add(MidSlot7)
            .Add(MidSlot8)

            .Add(LowSlot1)
            .Add(LowSlot2)
            .Add(LowSlot3)
            .Add(LowSlot4)
            .Add(LowSlot5)
            .Add(LowSlot6)
            .Add(LowSlot7)
            .Add(LowSlot8)

            .Add(ServiceSlot1)
            .Add(ServiceSlot2)
            .Add(ServiceSlot3)
            .Add(ServiceSlot4)
            .Add(ServiceSlot5)
            .Add(ServiceSlot6)

            .Add(RigSlot1)
            .Add(RigSlot2)
            .Add(RigSlot3)
        End With

        ' Save values
        HighSlotBaseX = HighSlot1.Location.X
        HighSlotBaseWidth = HighSlot1.Width
        HighSlotSpacing = HighSlot2.Location.X - (HighSlot1.Location.X + HighSlot1.Width)
        ServiceSlotBaseX = ServiceSlot1.Location.X
        ServiceSlotBaseWidth = ServiceSlot1.Width
        ServiceSlotSpacing = ServiceSlot2.Location.X - (ServiceSlot1.Location.X + ServiceSlot1.Width)

        SecurityCheckBoxes = New List(Of CheckBox)
        Call SecurityCheckBoxes.Add(chkHighSec)
        Call SecurityCheckBoxes.Add(chkLowSec)
        Call SecurityCheckBoxes.Add(chkNullSec)

        ' Select the security check box
        If FacilitySystemSecurity <= 0.0 Then
            chkNullSec.Checked = True
        ElseIf FacilitySystemSecurity < 0.45 Then
            chkLowSec.Checked = True
        Else
            chkHighSec.Checked = True
        End If

        'enable/ disable depending on the view
        If SelectedStructureView = FacilityView.NoView Then
            ' They aren't connected to a system
            chkHighSec.Enabled = True
            chkLowSec.Enabled = True
            chkNullSec.Enabled = True
        Else
            ' They are launching from a facility to view a system, don't let them change it
            chkHighSec.Enabled = False
            chkLowSec.Enabled = False
            chkNullSec.Enabled = False
        End If

        ' Get all data on structures for DB look ups first
        Call LoadStructureDBData()

        ' Add all the images to the image list
        Call LoadFittingImages()

        ' Load the facility default
        Call LoadStructure(InitName)

        ' Save these varibles for later
        SelectedCharacterID = CharacterID
        SelectedStructureView = FacilityLocation
        SelectedFacilityProductionType = FacilityType

        FirstLoad = False

    End Sub

    Private Sub frmCitadelFitting_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown

        With UserUpwellStructureSettings
            chkItemViewTypeHigh.Checked = .HighSlotsCheck
            chkItemViewTypeMedium.Checked = .MediumSlotsCheck
            chkItemViewTypeLow.Checked = .LowSlotsCheck
            chkItemViewTypeServices.Checked = .ServicesCheck

            chkRigTypeViewReprocessing.Checked = .ReprocessingRigsCheck
            chkRigTypeViewEngineering.Checked = .EngineeringRigsCheck
            chkRigTypeViewCombat.Checked = .CombatRigsCheck

            txtItemFilter.Text = .SearchFilterText

            chkIncludeFuelCosts.Checked = .IncludeFuelCostsCheck

            Select Case .FuelBlockType
                Case rbtnHeliumFuelBlock.Text
                    rbtnHeliumFuelBlock.Checked = True
                Case rbtnHydrogenFuelBlock.Text
                    rbtnHydrogenFuelBlock.Checked = True
                Case rbtnNitrogenFuelBlock.Text
                    rbtnNitrogenFuelBlock.Checked = True
                Case rbtnOxygenFuelBlock.Text
                    rbtnOxygenFuelBlock.Checked = True
            End Select

            Select Case .BuyBuildBlockOption
                Case rbtnBuildBlocks.Text
                    rbtnBuildBlocks.Checked = True
                Case rbtnBuyBlocks.Text
                    rbtnBuyBlocks.Checked = True
            End Select

        End With

    End Sub

    Private Sub LoadStructureDBData()
        Dim SQL As String = ""
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand

        SQL = "SELECT typeID, typeName, INVENTORY_GROUPS.groupID FROM INVENTORY_TYPES, INVENTORY_GROUPS WHERE INVENTORY_GROUPS.categoryID = 65 
                AND INVENTORY_TYPES.groupID = INVENTORY_GROUPS.groupid AND INVENTORY_TYPES.published = 1"

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        rsReader = DBCommand.ExecuteReader

        ' Clear the combo
        Call cmbUpwellStructureName.Items.Clear()

        While rsReader.Read()
            Dim TempData As UpwellStructureDBData

            TempData.TypeID = rsReader.GetInt32(0)
            TempData.Name = rsReader.GetString(1)
            TempData.GroupID = rsReader.GetInt32(2)

            Call StructureDBDataList.Add(TempData)

            ' Also add each to the combo box
            Call cmbUpwellStructureName.Items.Add(TempData.Name)

        End While

        rsReader.Close()

    End Sub

    Private Sub ServiceModuleListView_MouseDown(sender As Object, e As MouseEventArgs) Handles ServiceModuleListView.MouseDown
        ' Make sure we select the image
        Dim Selection As ListViewItem = ServiceModuleListView.GetItemAt(e.X, e.Y)

        If Not IsNothing(Selection) Then
            Dim ModuleTypeID As String = Selection.ImageKey

            If Not IsNothing(Selection) Then
                pbFloat.Image = FittingImages.Images(Selection.ImageKey)
                pbFloat.Name = Selection.Group.Name
                pbFloat.Tag = Selection.Group.Tag
            Else
                pbFloat.Image = Nothing
            End If

            If Not IsNothing(pbFloat.Image) Then
                pbFloat.Visible = True
                pbFloat.Location = New Point(e.X + ServiceModuleListView.Left, e.Y + ServiceModuleListView.Top)
                ' Now select the image and connect it to the mouse cursor
                SendMessage(pbFloat.Handle.ToInt32, WM_NCLBUTTONDOWN, HTCAPTION, 0&)
            Else
                pbFloat.Visible = False
            End If

            pbFloat.Visible = False

            Dim SlotLocation As Point
            Dim WHAdjust As Integer = 64
            Dim MP As Point = PointToClient(MousePosition)

            ' Loop through all the picture boxes and update the one they clicked over
            For Each Slot In SlotPictureBoxList

                SlotLocation = Slot.Location
                SlotLocation.X += tabUpwellStructure.Left
                SlotLocation.Y += tabUpwellStructure.Top

                ' See if they dropped the image on a fitting slot and change the selected item
                If MP.X > SlotLocation.X And MP.X < SlotLocation.X + WHAdjust And
                    MP.Y > SlotLocation.Y And MP.Y < SlotLocation.Y + WHAdjust Then
                    Dim FloatSlot As String = CStr(pbFloat.Tag)

                    If FloatSlot.Contains(Slot.Name.Substring(0, Len(Slot.Name) - 1)) Then

                        If Not CheckSlots(ModuleTypeID) Then
                            Exit Sub
                        End If

                        ' Set the image info
                        Slot.Image = pbFloat.Image
                        Slot.Image.Tag = ModuleTypeID
                        Slot.Tag = pbFloat.Name

                        ' Update the slot stats
                        Call UpdateUpwellStructureStats()
                        ' Update the launcher slots if added a launcher
                        Call UpdateLauncherSlots(False, ModuleTypeID)
                        ' Done updating
                        Exit For
                    End If
                End If
            Next
        End If
    End Sub

    ' Loads the image in the first free slot if available - use for double-click an item
    Private Sub LoadImageInFreeSlot()
        Dim Selection As ListViewItem = ServiceModuleListView.SelectedItems(0)

        If Not IsNothing(Selection) Then
            Dim ModuleTypeID As String = Selection.ImageKey

            ' Loop through all the picture boxes and add the first one that is empty
            For Each Slot In SlotPictureBoxList
                Dim FloatSlot As String = CStr(Selection.Group.Tag)

                If FloatSlot.Contains(Slot.Name.Substring(0, Len(Slot.Name) - 1)) Then

                    If Not CheckSlots(ModuleTypeID) Then
                        Exit Sub
                    End If

                    ' Set the image info if nothing, then exit
                    If IsNothing(Slot.Image) Then
                        Slot.Image = FittingImages.Images(ModuleTypeID)
                        Slot.Image.Tag = ModuleTypeID
                        ' Update the slot stats
                        Call UpdateUpwellStructureStats()
                        ' Update the launcher slots if added a launcher
                        Call UpdateLauncherSlots(False, ModuleTypeID)
                        ' Done updating
                        Exit For
                    End If
                End If
            Next
        End If
    End Sub

    Private Function CheckSlots(ByVal ModuleTypeID As String) As Boolean
        ' Only drop if over the right slot
        If RigFound(ModuleTypeID) Then
            ' They already used this rig, so don't allow
            Return False
        End If

        If ServiceFound(ModuleTypeID) Then
            ' Already have this service installed
            Return False
        End If

        ' Check launchers
        If IsMissileLauncher(ModuleTypeID) Then
            If UpwellStructureStats.LauncherSlots = 0 Then
                ' They don't have any slots left
                Return False
            End If
        End If

        Return True

    End Function

    ' Determines if the item is a missile launcher or not to adjust weapon slots
    Private Function IsMissileLauncher(TypeID As String) As Boolean
        Dim SQL As String = String.Format("SELECT * FROM type_effects WHERe typeid = {0} AND effectID = {1}", TypeID, UsesMissilesEffectID)
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        rsReader = DBCommand.ExecuteReader

        If rsReader.Read() Then
            ' Found it
            rsReader.Close()
            Return True
        Else
            rsReader.Close()
            Return False
        End If

    End Function

    ' Sees if the rig is already used or not
    Private Function RigFound(TypeID As String) As Boolean
        Dim CurrentRigTypes As New List(Of String)

        If Not IsNothing(RigSlot1.Image) Then
            CurrentRigTypes.Add(CStr(RigSlot1.Image.Tag))
        End If
        If Not IsNothing(RigSlot2.Image) Then
            CurrentRigTypes.Add(CStr(RigSlot2.Image.Tag))
        End If
        If Not IsNothing(RigSlot3.Image) Then
            CurrentRigTypes.Add(CStr(RigSlot3.Image.Tag))
        End If

        If CurrentRigTypes.Contains(TypeID) Then
            Return True
        Else
            Return False
        End If

    End Function

    Private Function ServiceFound(TypeID As String) As Boolean
        Dim CurrentServiceTypes As New List(Of String)

        If Not IsNothing(ServiceSlot1.Image) Then
            CurrentServiceTypes.Add(CStr(ServiceSlot1.Image.Tag))
        End If
        If Not IsNothing(ServiceSlot2.Image) Then
            CurrentServiceTypes.Add(CStr(ServiceSlot2.Image.Tag))
        End If
        If Not IsNothing(ServiceSlot3.Image) Then
            CurrentServiceTypes.Add(CStr(ServiceSlot3.Image.Tag))
        End If
        If Not IsNothing(ServiceSlot4.Image) Then
            CurrentServiceTypes.Add(CStr(ServiceSlot4.Image.Tag))
        End If
        If Not IsNothing(ServiceSlot5.Image) Then
            CurrentServiceTypes.Add(CStr(ServiceSlot5.Image.Tag))
        End If
        If Not IsNothing(ServiceSlot6.Image) Then
            CurrentServiceTypes.Add(CStr(ServiceSlot6.Image.Tag))
        End If

        If CurrentServiceTypes.Contains(TypeID) Then
            Return True
        Else
            '' Special case, check if they have a research lab loaded already, only allow one
            'If CurrentServiceTypes.Contains(ServiceResearchLabI) And TypeID = ServiceHyasyodaLab Or
            '        CurrentServiceTypes.Contains(ServiceHyasyodaLab) And TypeID = ServiceResearchLabI Then
            '    Return True
            'Else
            Return False
            'End If
        End If

    End Function

    Private Sub LoadStructure(ByVal SentCitadelName As String)

        CitadelName = SentCitadelName

        ' First get the data to use
        SelectedUpwellStructure = GetCitadelData(SentCitadelName)
        ' Set the combo text
        cmbUpwellStructureName.Text = SelectedUpwellStructure.Name
        ' Load the image
        Call LoadStructureRenderImage()
        ' Refresh the items list
        Call UpdateFittingImages()
        ' Set the slots
        Call UpdateCitadelSlots()
        ' Set the stats
        Call LoadUpwellStuctureStats()

    End Sub

    Private Sub StripFitting()

        HighSlot1.Image = Nothing
        HighSlot2.Image = Nothing
        HighSlot3.Image = Nothing
        HighSlot4.Image = Nothing
        HighSlot5.Image = Nothing
        HighSlot6.Image = Nothing
        HighSlot7.Image = Nothing
        HighSlot8.Image = Nothing

        MidSlot1.Image = Nothing
        MidSlot2.Image = Nothing
        MidSlot3.Image = Nothing
        MidSlot4.Image = Nothing
        MidSlot5.Image = Nothing
        MidSlot6.Image = Nothing
        MidSlot7.Image = Nothing
        MidSlot8.Image = Nothing

        LowSlot1.Image = Nothing
        LowSlot2.Image = Nothing
        LowSlot3.Image = Nothing
        LowSlot4.Image = Nothing
        LowSlot5.Image = Nothing
        LowSlot6.Image = Nothing
        LowSlot7.Image = Nothing
        LowSlot8.Image = Nothing

        ServiceSlot1.Image = Nothing
        ServiceSlot2.Image = Nothing
        ServiceSlot3.Image = Nothing
        ServiceSlot4.Image = Nothing
        ServiceSlot5.Image = Nothing
        ServiceSlot6.Image = Nothing

        RigSlot1.Image = Nothing
        RigSlot2.Image = Nothing
        RigSlot3.Image = Nothing

        ' init the upwell structure stats
        Call LoadUpwellStuctureStats()

    End Sub

    ' Load the image into the background
    Private Sub LoadStructureRenderImage()

        For Each UPWStructure In StructureDBDataList
            ' Look for the name and then load the render image from the typeID (should be in images folder)
            If UPWStructure.Name = cmbUpwellStructureName.Text Then
                If System.IO.File.Exists(BPImageFilePath & UPWStructure.TypeID & ".png") Then
                    StructurePicture.Image = Image.FromFile(BPImageFilePath & UPWStructure.TypeID & ".png")
                Else
                    StructurePicture.Image = Nothing
                End If
                Exit For
            End If
        Next

        StructurePicture.Refresh()
        Application.DoEvents()

    End Sub

    ' Gets and returns the upwell structure data
    Private Function GetCitadelData(ByVal LookupName As String) As UpwellStructureDBData
        Dim SQL As String = ""
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand

        SQL = "SELECT typeID, groupID FROM INVENTORY_TYPES "
        SQL &= "WHERE INVENTORY_TYPES.published <> 0 AND typeName = '" & LookupName & "'"

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        rsReader = DBCommand.ExecuteReader

        If rsReader.Read() Then
            Dim TempData As UpwellStructureDBData

            TempData.TypeID = rsReader.GetInt32(0)
            TempData.Name = LookupName
            TempData.GroupID = rsReader.GetInt32(1)
            rsReader.Close()

            Return TempData

        Else
            Return Nothing
        End If

    End Function

    ' Clear and Set the slots to match the upwell structure we are using
    Private Sub UpdateCitadelSlots()
        Dim SQL As String = ""
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand
        Dim AID As Integer

        ' Query all the stats for the selected Upwell Structure and process slots
        SQL = "Select attributeID, COALESCE(valueint, valuefloat) As Value "
        SQL &= "FROM TYPE_ATTRIBUTES, INVENTORY_TYPES "
        SQL &= "WHERE attributeID In (" & ItemAttributes.hiSlots & "," & ItemAttributes.medSlots & "," & ItemAttributes.lowSlots & "," & ItemAttributes.serviceSlots & "," & ItemAttributes.rigSlots & ") "
        SQL &= "And INVENTORY_TYPES.typeID = TYPE_ATTRIBUTES.typeID And typeName = '" & cmbUpwellStructureName.Text & "'"

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        rsReader = DBCommand.ExecuteReader

        While rsReader.Read()
            AID = rsReader.GetInt32(0)
            If AID = ItemAttributes.hiSlots Then
                Call SetHighSlots(CInt(rsReader.GetValue(1)))
            ElseIf AID = ItemAttributes.medSlots Then
                Call SetMidSlots(CInt(rsReader.GetValue(1)))
            ElseIf AID = ItemAttributes.lowSlots Then
                Call SetLowSlots(CInt(rsReader.GetValue(1)))
            ElseIf AID = ItemAttributes.rigSlots Then
                Call SetRigSlots(CInt(rsReader.GetValue(1)))
            ElseIf AID = ItemAttributes.serviceSlots Then
                Call SetServiceSlots(CInt(rsReader.GetValue(1)))
            End If
        End While

        rsReader.Close()

    End Sub

    ' Updates the stats after a module is chosen
    Private Sub LoadUpwellStuctureStats(Optional IgnoreLabelUpdate As Boolean = False)
        Dim Stats As New List(Of AttributeRecord)
        Dim AttributesLookup As New EVEAttributes

        ' Get all the stats for the upwell structure 
        Stats = AttributesLookup.GetAttributes(SelectedUpwellStructure.Name)

        ' Loop through and get the stuff we want, save it locally for update
        For Each Stat In Stats
            Select Case Stat.ID
                Case ItemAttributes.cpuOutput
                    UpwellStructureStats.MaxCPU = Stat.Value
                    UpwellStructureStats.CPU = Stat.Value
                Case ItemAttributes.powerOutput
                    UpwellStructureStats.MaxPG = Stat.Value
                    UpwellStructureStats.PG = Stat.Value
                Case ItemAttributes.upgradeCapacity ' Calibration
                    UpwellStructureStats.MaxCalibration = Stat.Value
                    UpwellStructureStats.Calibration = Stat.Value
                Case ItemAttributes.capacitorCapacity
                    UpwellStructureStats.Capacitor = Stat.Value
                    UpwellStructureStats.MaxCapacitor = Stat.Value
                Case ItemAttributes.rechargeRate
                    UpwellStructureStats.CapacitorRechargeRate = 100
                    UpwellStructureStats.BaseCapRechargeRate = Stat.Value
                Case ItemAttributes.launcherSlotsLeft
                    If Not IgnoreLabelUpdate Then
                        ' Only update this if we are updating the label too
                        UpwellStructureStats.LauncherSlots = CInt(Stat.Value)
                    End If
            End Select
        Next

        ' Fuel is always 0 to start with no limit
        UpwellStructureStats.ServiceModuleFuelBPH = 0

        ' Update the stats
        If Not IgnoreLabelUpdate Then
            Call UpdateUpwellStructureStatLabels()
        End If

    End Sub

    ' Updates the label stats of the upwell structure to include any items selected and installed
    Private Sub UpdateUpwellStructureStatLabels()

        ' Update the labels
        lblCPU.Text = FormatNumber(UpwellStructureStats.CPU) & " / " & FormatNumber(UpwellStructureStats.MaxCPU)
        If UpwellStructureStats.CPU < 0 Then
            lblCPU.ForeColor = Color.Red
        Else
            lblCPU.ForeColor = Color.Black
        End If

        lblPowerGrid.Text = FormatNumber(UpwellStructureStats.PG) & " / " & FormatNumber(UpwellStructureStats.MaxPG)
        If UpwellStructureStats.PG < 0 Then
            lblPowerGrid.ForeColor = Color.Red
        Else
            lblPowerGrid.ForeColor = Color.Black
        End If

        lblCalibration.Text = FormatNumber(UpwellStructureStats.Calibration) & " / " & FormatNumber(UpwellStructureStats.MaxCalibration)
        If UpwellStructureStats.Calibration < 0 Then
            lblCalibration.ForeColor = Color.Red
        Else
            lblCalibration.ForeColor = Color.Black
        End If

        lblCapacitor.Text = FormatNumber(UpwellStructureStats.Capacitor) & " / " & FormatNumber(UpwellStructureStats.MaxCapacitor)
        If UpwellStructureStats.Capacitor < 0 Then
            lblCapacitor.ForeColor = Color.Red
        Else
            lblCapacitor.ForeColor = Color.Black
        End If

        lblLauncherSlots.Text = "Launcher Slots: " & CStr(UpwellStructureStats.LauncherSlots)

        ' Update the fuel costs label
        Call UpdateFuelCostLabels()

    End Sub

    Private Sub UpdateUpwellStructureStats()
        Dim InstalledSlots As New List(Of StructureModule)
        Dim Attributes As New List(Of AttributeRecord)
        Dim AttribLookup As New EVEAttributes

        InstalledSlots = GetInstalledSlots()

        ' Reset the totals each time before updating
        Call LoadUpwellStuctureStats(True)

        For Each Item In InstalledSlots
            ' Look up the attributes for each slot and update the stats we want
            Attributes = AttribLookup.GetAttributes(Item.typeID)

            For Each Attribute In Attributes
                Select Case Attribute.ID
                    Case ItemAttributes.power
                        UpwellStructureStats.PG -= Attribute.Value
                    Case ItemAttributes.cpu
                        UpwellStructureStats.CPU -= Attribute.Value
                    Case ItemAttributes.capacitorNeed
                        UpwellStructureStats.Capacitor -= Attribute.Value
                    Case ItemAttributes.upgradeCost ' Calibration
                        UpwellStructureStats.Calibration -= Attribute.Value
                    Case ItemAttributes.cpuMultiplier
                        UpwellStructureStats.MaxCPU = UpwellStructureStats.MaxCPU * Attribute.Value
                    Case ItemAttributes.powerOutputMultiplier
                        UpwellStructureStats.MaxPG = UpwellStructureStats.MaxPG * Attribute.Value
                    Case ItemAttributes.serviceModuleFuelAmount
                        UpwellStructureStats.ServiceModuleFuelBPH -= CInt(Attribute.Value)
                End Select
            Next
        Next

        ' Update the stat labels
        Call UpdateUpwellStructureStatLabels()

        ' Update the bonuses from items installed
        Call UpdateUpwellStructureBonuses()

    End Sub

    ' If true, increments the launcher slots, else decrements
    Private Sub UpdateLauncherSlots(ByVal Increment As Boolean, ByVal ModuleTypeID As String)
        ' Update number of launchers
        If IsMissileLauncher(ModuleTypeID) Then
            If Not Increment Then
                If UpwellStructureStats.LauncherSlots > 0 Then
                    UpwellStructureStats.LauncherSlots -= 1
                End If

            Else
                UpwellStructureStats.LauncherSlots += 1
            End If
        End If

        lblLauncherSlots.Text = "Launcher Slots: " & CStr(UpwellStructureStats.LauncherSlots)

    End Sub

    ' Returns the list of moduleIDs installed in the upwell structure
    Private Function GetInstalledSlots() As List(Of StructureModule)
        Dim ReturnItems As New List(Of StructureModule)
        Dim Entry As StructureModule

        ' Go through all slots and return the typeIDs (saved in tag of image) for each installed item
        If Not IsNothing(HighSlot1.Image) Then
            Entry.typeID = CInt(HighSlot1.Image.Tag)
            Entry.moduleType = CStr(HighSlot1.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot2.Image) Then
            Entry.typeID = CInt(HighSlot2.Image.Tag)
            Entry.moduleType = CStr(HighSlot2.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot3.Image) Then
            Entry.typeID = CInt(HighSlot3.Image.Tag)
            Entry.moduleType = CStr(HighSlot3.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot4.Image) Then
            Entry.typeID = CInt(HighSlot4.Image.Tag)
            Entry.moduleType = CStr(HighSlot4.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot5.Image) Then
            Entry.typeID = CInt(HighSlot5.Image.Tag)
            Entry.moduleType = CStr(HighSlot5.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot6.Image) Then
            Entry.typeID = CInt(HighSlot6.Image.Tag)
            Entry.moduleType = CStr(HighSlot6.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot7.Image) Then
            Entry.typeID = CInt(HighSlot7.Image.Tag)
            Entry.moduleType = CStr(HighSlot7.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(HighSlot8.Image) Then
            Entry.typeID = CInt(HighSlot8.Image.Tag)
            Entry.moduleType = CStr(HighSlot8.Tag)
            ReturnItems.Add(Entry)
        End If

        If Not IsNothing(MidSlot1.Image) Then
            Entry.typeID = CInt(MidSlot1.Image.Tag)
            Entry.moduleType = CStr(MidSlot1.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot2.Image) Then
            Entry.typeID = CInt(MidSlot2.Image.Tag)
            Entry.moduleType = CStr(MidSlot2.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot3.Image) Then
            Entry.typeID = CInt(MidSlot3.Image.Tag)
            Entry.moduleType = CStr(MidSlot3.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot4.Image) Then
            Entry.typeID = CInt(MidSlot4.Image.Tag)
            Entry.moduleType = CStr(MidSlot4.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot5.Image) Then
            Entry.typeID = CInt(MidSlot5.Image.Tag)
            Entry.moduleType = CStr(MidSlot5.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot6.Image) Then
            Entry.typeID = CInt(MidSlot6.Image.Tag)
            Entry.moduleType = CStr(MidSlot6.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot7.Image) Then
            Entry.typeID = CInt(MidSlot7.Image.Tag)
            Entry.moduleType = CStr(MidSlot7.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(MidSlot8.Image) Then
            Entry.typeID = CInt(MidSlot8.Image.Tag)
            Entry.moduleType = CStr(MidSlot8.Tag)
            ReturnItems.Add(Entry)
        End If

        If Not IsNothing(LowSlot1.Image) Then
            Entry.typeID = CInt(LowSlot1.Image.Tag)
            Entry.moduleType = CStr(LowSlot1.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot2.Image) Then
            Entry.typeID = CInt(LowSlot2.Image.Tag)
            Entry.moduleType = CStr(LowSlot2.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot3.Image) Then
            Entry.typeID = CInt(LowSlot3.Image.Tag)
            Entry.moduleType = CStr(LowSlot3.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot4.Image) Then
            Entry.typeID = CInt(HighSlot1.Image.Tag)
            Entry.moduleType = CStr(HighSlot1.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot5.Image) Then
            Entry.typeID = CInt(LowSlot5.Image.Tag)
            Entry.moduleType = CStr(LowSlot5.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot6.Image) Then
            Entry.typeID = CInt(LowSlot6.Image.Tag)
            Entry.moduleType = CStr(LowSlot6.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot7.Image) Then
            Entry.typeID = CInt(LowSlot7.Image.Tag)
            Entry.moduleType = CStr(LowSlot7.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(LowSlot8.Image) Then
            Entry.typeID = CInt(LowSlot8.Image.Tag)
            Entry.moduleType = CStr(LowSlot8.Tag)
            ReturnItems.Add(Entry)
        End If

        If Not IsNothing(RigSlot1.Image) Then
            Entry.typeID = CInt(RigSlot1.Image.Tag)
            Entry.moduleType = CStr(RigSlot1.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(RigSlot2.Image) Then
            Entry.typeID = CInt(RigSlot2.Image.Tag)
            Entry.moduleType = CStr(RigSlot2.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(RigSlot3.Image) Then
            Entry.typeID = CInt(RigSlot3.Image.Tag)
            Entry.moduleType = CStr(RigSlot3.Tag)
            ReturnItems.Add(Entry)
        End If

        If Not IsNothing(ServiceSlot1.Image) Then
            Entry.typeID = CInt(ServiceSlot1.Image.Tag)
            Entry.moduleType = CStr(ServiceSlot1.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(ServiceSlot2.Image) Then
            Entry.typeID = CInt(ServiceSlot2.Image.Tag)
            Entry.moduleType = CStr(ServiceSlot2.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(ServiceSlot3.Image) Then
            Entry.typeID = CInt(ServiceSlot3.Image.Tag)
            Entry.moduleType = CStr(ServiceSlot3.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(ServiceSlot4.Image) Then
            Entry.typeID = CInt(ServiceSlot4.Image.Tag)
            Entry.moduleType = CStr(ServiceSlot4.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(ServiceSlot5.Image) Then
            Entry.typeID = CInt(ServiceSlot5.Image.Tag)
            Entry.moduleType = CStr(ServiceSlot5.Tag)
            ReturnItems.Add(Entry)
        End If
        If Not IsNothing(ServiceSlot6.Image) Then
            Entry.typeID = CInt(ServiceSlot6.Image.Tag)
            Entry.moduleType = CStr(ServiceSlot6.Tag)
            ReturnItems.Add(Entry)
        End If

        Return ReturnItems

    End Function

    Private Function GetFuelCost(ByVal NumBlocks As Integer) As String
        Return ""
    End Function

    Private Sub UpdateFuelCostLabels()
        ' If they want fuel cost
        If chkIncludeFuelCosts.Checked Then
            lblServiceModuleBPH.Text = FormatNumber(UpwellStructureStats.ServiceModuleFuelBPH, 0) & " Blocks per Hour"
            lblServiceModuleFCPH.Text = GetFuelCost(UpwellStructureStats.ServiceModuleFuelBPH)
        Else
            lblServiceModuleBPH.Text = "-"
            lblServiceModuleFCPH.Text = "-"
        End If
    End Sub

    ' Loads the images for fittings in the image lists
    Private Sub LoadFittingImages()
        Dim SQL As String = ""
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand

        Try

            SQL = "SELECT typeID, typeName FROM INVENTORY_TYPES, INVENTORY_GROUPS "
            SQL &= "WHERE INVENTORY_TYPES.groupID = INVENTORY_GROUPS.groupID AND ABS(categoryID) = 66 " ' I save rigs as -66
            SQL &= "AND INVENTORY_TYPES.published <> 0"

            DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
            rsReader = DBCommand.ExecuteReader

            Dim myImage As Image
            Dim typeID As String
            Dim typeName As String

            While rsReader.Read()
                ' Add to the image list, and put in view with names
                typeID = CStr(rsReader.GetInt32(0))
                typeName = rsReader.GetString(1)
                If System.IO.File.Exists(BPImageFilePath & typeID & "_64.png") Then
                    myImage = Image.FromFile(BPImageFilePath & typeID & "_64.png")

                    Call FittingImages.Images.Add(typeID, myImage)
                Else
                    Debug.Print(BPImageFilePath & typeID & "_64.png")
                End If
            End While

            rsReader.Close()

        Catch ex As Exception
            Application.DoEvents()
        End Try
    End Sub

    ' Updates all the fitting images based on the check boxes in the list view
    Private Sub UpdateFittingImages()

        If Not FirstLoad Then

            ' Clear current images
            ServiceModuleListView.Items.Clear()

            Dim SQL As String = ""
            Dim RigString As String = ""
            Dim SlotString As String = ""
            Dim SQLList As New List(Of String)
            Dim rsReader As SQLiteDataReader
            Dim DBCommand As SQLiteCommand

            SQL = "SELECT INVENTORY_TYPES.typeID, INVENTORY_GROUPS.groupID, typeName, "
            SQL &= "CASE WHEN effectID IS NULL THEN -1 ELSE effectID END AS EffID, groupName, "
            SQL &= "CASE WHEN COALESCE(valuefloat, valueint) IS NULL THEN -1 ELSE COALESCE(valuefloat, valueint) END AS RIG_SIZE, "
            SQL &= "CASE WHEN (SELECT COALESCE(valuefloat, valueint) FROM TYPE_ATTRIBUTES "
            SQL &= "WHERE typeID = INVENTORY_TYPES.typeID AND attributeID = " & ItemAttributes.disallowInHighSec & ") = 1 THEN 0 ELSE 1 END AS ALLOW_IN_HS "
            SQL &= "FROM INVENTORY_GROUPS, INVENTORY_TYPES "
            SQL &= "LEFT JOIN TYPE_EFFECTS ON INVENTORY_TYPES.typeID = TYPE_EFFECTS.typeID AND effectID IN (12,13,11) "
            SQL &= "LEFT JOIN TYPE_ATTRIBUTES ON INVENTORY_TYPES.typeID = TYPE_ATTRIBUTES.typeID "
            SQL &= "AND attributeID = " & CStr(ItemAttributes.rigSize) & " "
            SQL &= "WHERE INVENTORY_TYPES.groupID = INVENTORY_GROUPS.groupID And ABS(categoryID) = 66 " ' I save structure rigs as -66
            SQL &= "And INVENTORY_TYPES.published <> 0 "

            ' Add text first
            If Trim(txtItemFilter.Text) <> "" Then
                SQL &= "And " & GetSearchText(txtItemFilter.Text, "typeName") & " "
            End If

            If chkItemViewTypeServices.Checked Then
                ' Add the sql
                Call SQLList.Add("(INVENTORY_TYPES.groupID In (1321, 1322, 1415, 1717)) ")
            End If

            ' Process high, medium, and low slots together
            If chkItemViewTypeHigh.Checked Then
                SlotString &= CStr(SlotSizes.HighSlot) & ","
            End If

            If chkItemViewTypeMedium.Checked Then
                SlotString &= CStr(SlotSizes.MediumSlot) & ","
            End If

            If chkItemViewTypeLow.Checked Then
                SlotString &= CStr(SlotSizes.LowSlot) & ","
            End If

            If SlotString <> "" Then
                SlotString = SlotString.Substring(0, Len(SlotString) - 1)
                SlotString = "(EffID In (" & SlotString & "))"
                ' Add the sql
                Call SQLList.Add(SlotString)
            End If

            If chkRigTypeViewCombat.Checked Or chkRigTypeViewEngineering.Checked Or chkRigTypeViewReprocessing.Checked _
                Or chkRigTypeViewDrilling.Checked Or chkRigTypeViewReaction.Checked Then
                If chkRigTypeViewCombat.Checked Then
                    Call SQLList.Add("(groupName Like '%Combat Rig%')")
                End If

                If chkRigTypeViewEngineering.Checked Then
                    Call SQLList.Add("(groupName LIKE '%Engineering Rig%')")
                End If

                If chkRigTypeViewReprocessing.Checked Then
                    Call SQLList.Add("(groupName LIKE '%Resource Rig%')")
                End If

                If chkRigTypeViewReaction.Checked Then
                    Call SQLList.Add("(groupName LIKE '%Reactor Rig%')")
                End If

                If chkRigTypeViewDrilling.Checked Then
                    Call SQLList.Add("(groupName LIKE '%Drilling Rig%')")
                End If

                Dim Attrib As New EVEAttributes

                ' Add the check for rig size to limit, -1 is the default value
                SQL &= "AND RIG_SIZE IN (-1," & CInt(Attrib.GetAttribute(SelectedUpwellStructure.TypeID, ItemAttributes.rigSize)) & ") "

            End If

            ' Set the SQL
            If SQLList.Count > 0 Then
                SQL &= "AND ("
                For Each entry In SQLList
                    SQL &= "(" & entry & ") OR "
                Next
                ' Strip last OR
                SQL = SQL.Substring(0, Len(SQL) - 4)
                SQL &= ")"
            Else
                Exit Sub
            End If

            DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
            rsReader = DBCommand.ExecuteReader

            While rsReader.Read()
                Dim GID As Integer = rsReader.GetInt32(1)
                Dim EID As Integer = rsReader.GetInt32(3)
                Dim LVI As New ListViewItem

                ' Only add if it can be fit to the selected upwell structure and it meets the space requirements
                If StructureCanFitItem(SelectedUpwellStructure.TypeID, SelectedUpwellStructure.GroupID, rsReader.GetInt32(0)) _
                    And ((chkHighSec.Checked = True And rsReader.GetInt32(6) <> 0) Or chkHighSec.Checked = False) Then

                    '& CStr(ItemAttributes.disallowInHighSec) & ") "
                    If GID = 1321 Or GID = 1322 Or GID = 1415 Or GID = 1717 Then
                        LVI.Group = ServiceModuleListView.Groups(0) ' 0 is services
                    ElseIf EID = SlotSizes.HighSlot Then
                        LVI.Group = ServiceModuleListView.Groups(1) ' 1 is high
                    ElseIf EID = SlotSizes.MediumSlot Then
                        LVI.Group = ServiceModuleListView.Groups(2) ' 2 is medium
                    ElseIf EID = SlotSizes.LowSlot Then
                        LVI.Group = ServiceModuleListView.Groups(3) ' 3 is low
                    Else
                        ' Rigs
                        If rsReader.GetString(4).Contains("Combat") Then
                            LVI.Group = ServiceModuleListView.Groups(4) ' 4 is Combat rigs
                        ElseIf rsReader.GetString(4).Contains("Reprocessing") Or rsReader.GetString(4).Contains("Grading") Then
                            LVI.Group = ServiceModuleListView.Groups(5) ' 5 is Reprocessing rigs
                        ElseIf rsReader.GetString(4).Contains("Engineering") Then
                            LVI.Group = ServiceModuleListView.Groups(6) ' 6 is Engineering rigs
                        ElseIf rsReader.GetString(4).Contains("Reaction") Then
                            LVI.Group = ServiceModuleListView.Groups(7) ' 7 is Reaction rigs
                        ElseIf rsReader.GetString(4).Contains("Drilling") Then
                            LVI.Group = ServiceModuleListView.Groups(8) ' 8 is Drilling rigs
                        End If
                    End If

                    ' add the image
                    LVI.ImageKey = CStr(rsReader.GetInt32(0))
                    LVI.Text = rsReader.GetString(2)
                    ServiceModuleListView.Items.Add(LVI)
                End If

            End While

        End If

    End Sub

    ' Reads the attributes to see if the itemID sent can be fit to the upwell structureID sent
    Private Function StructureCanFitItem(ByVal StructureTypeID As Integer, ByVal StructureGroupID As Integer, ByVal ItemTypeID As Integer) As Boolean
        Dim SQL As String = ""
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand

        SQL = "SELECT COALESCE(valuefloat, valueint) AS STRUCTURE_ID FROM TYPE_ATTRIBUTES, ATTRIBUTE_TYPES "
        SQL &= "WHERE TYPE_ATTRIBUTES.typeID = {0} AND ATTRIBUTE_TYPES.attributeID = TYPE_ATTRIBUTES.attributeID "
        SQL &= "AND (attributeName LIKE 'canFitShipType%' OR attributeName LIKE 'canFitShipGroup%')"
        ' Add typeid to look up
        SQL = String.Format(SQL, ItemTypeID)

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        rsReader = DBCommand.ExecuteReader

        While rsReader.Read()
            Dim IDtoCheck As Integer = CInt(rsReader.GetValue(0))
            If IDtoCheck = StructureTypeID Or IDtoCheck = StructureGroupID Then
                Return True
            End If
        End While

        ' Not found
        Return False

    End Function

    ' Moves the high slots to center based on the rig slot images
    Private Sub ShiftHighSlotImages()

        ' Move the top 5 over to match the rig slot locations
        HighSlot1.Left = RigSlot2.Left
        Call AlignHighSlotsfromBase()

    End Sub

    Private Sub ResetHighSlotImages()

        ' Move the top 5 back since 6 and above won't move
        HighSlot1.Left = HighSlotBaseX
        Call AlignHighSlotsfromBase()

    End Sub

    Private Sub AlignHighSlotsfromBase()
        ' Aligns the high slots based on the first high slot position
        HighSlot2.Left = HighSlot1.Left + HighSlotSpacing + HighSlotBaseWidth
        HighSlot3.Left = HighSlot1.Left - HighSlotBaseWidth - HighSlotSpacing
        HighSlot4.Left = HighSlot2.Left + HighSlotSpacing + HighSlotBaseWidth
        HighSlot5.Left = HighSlot3.Left - HighSlotBaseWidth - HighSlotSpacing
    End Sub

    ' Moves the high slots to center based on the rig slot images
    Private Sub ShiftServiceSlotImages()

        ' Move the top 5 over to match the rig slot locations
        ServiceSlot1.Left = RigSlot2.Left
        Call AlignServiceSlotsfromBase()

    End Sub

    Private Sub ResetServiceSlotImages()

        ' Move the top 5 back since 6 and above won't move
        ServiceSlot1.Left = ServiceSlotBaseX
        Call AlignServiceSlotsfromBase()

    End Sub

    Private Sub AlignServiceSlotsfromBase()
        ' Aligns the service slots based on the first high slot position
        ServiceSlot2.Left = ServiceSlot1.Left + ServiceSlotSpacing + ServiceSlotBaseWidth
        ServiceSlot3.Left = ServiceSlot1.Left - ServiceSlotBaseWidth - ServiceSlotSpacing
        ServiceSlot4.Left = ServiceSlot2.Left + ServiceSlotSpacing + ServiceSlotBaseWidth
        ServiceSlot5.Left = ServiceSlot3.Left - ServiceSlotBaseWidth - ServiceSlotSpacing
    End Sub

    Private Sub SetHighSlots(Slots As Integer)

        ' Init slots
        HighSlot1.Visible = False
        HighSlot2.Visible = False
        HighSlot3.Visible = False
        HighSlot4.Visible = False
        HighSlot5.Visible = False
        HighSlot6.Visible = False
        HighSlot7.Visible = False
        HighSlot8.Visible = False

        HighSlot1.Image = Nothing
        HighSlot2.Image = Nothing
        HighSlot3.Image = Nothing
        HighSlot4.Image = Nothing
        HighSlot5.Image = Nothing
        HighSlot6.Image = Nothing
        HighSlot7.Image = Nothing
        HighSlot8.Image = Nothing

        For i = 1 To Slots
            Select Case i
                Case 1
                    HighSlot1.Visible = True
                Case 2
                    HighSlot2.Visible = True
                Case 3
                    HighSlot3.Visible = True
                Case 4
                    HighSlot4.Visible = True
                Case 5
                    HighSlot5.Visible = True
                Case 6
                    HighSlot6.Visible = True
                Case 7
                    HighSlot7.Visible = True
                Case 8
                    HighSlot8.Visible = True
            End Select
        Next

        If Slots Mod 2 > 0 And Slots < 6 Then
            ' Move the slots if we are on the first line
            Call ShiftHighSlotImages()
        Else
            ' Reset them to the base positions
            Call ResetHighSlotImages()
        End If

    End Sub

    Private Sub SetMidSlots(Slots As Integer)

        ' Init slots
        MidSlot1.Visible = False
        MidSlot2.Visible = False
        MidSlot3.Visible = False
        MidSlot4.Visible = False
        MidSlot5.Visible = False
        MidSlot6.Visible = False
        MidSlot7.Visible = False
        MidSlot8.Visible = False

        MidSlot1.Image = Nothing
        MidSlot2.Image = Nothing
        MidSlot3.Image = Nothing
        MidSlot4.Image = Nothing
        MidSlot5.Image = Nothing
        MidSlot6.Image = Nothing
        MidSlot7.Image = Nothing
        MidSlot8.Image = Nothing

        For i = 1 To Slots
            Select Case i
                Case 1
                    MidSlot1.Visible = True
                Case 2
                    MidSlot2.Visible = True
                Case 3
                    MidSlot3.Visible = True
                Case 4
                    MidSlot4.Visible = True
                Case 5
                    MidSlot5.Visible = True
                Case 6
                    MidSlot6.Visible = True
                Case 7
                    MidSlot7.Visible = True
                Case 8
                    MidSlot8.Visible = True
            End Select
        Next
    End Sub

    Private Sub SetLowSlots(Slots As Integer)

        ' Init slots
        LowSlot1.Visible = False
        LowSlot2.Visible = False
        LowSlot3.Visible = False
        LowSlot4.Visible = False
        LowSlot5.Visible = False
        LowSlot6.Visible = False
        LowSlot7.Visible = False
        LowSlot8.Visible = False

        LowSlot1.Image = Nothing
        LowSlot2.Image = Nothing
        LowSlot3.Image = Nothing
        LowSlot4.Image = Nothing
        LowSlot5.Image = Nothing
        LowSlot6.Image = Nothing
        LowSlot7.Image = Nothing
        LowSlot8.Image = Nothing

        For i = 1 To Slots
            Select Case i
                Case 1
                    LowSlot1.Visible = True
                Case 2
                    LowSlot2.Visible = True
                Case 3
                    LowSlot3.Visible = True
                Case 4
                    LowSlot4.Visible = True
                Case 5
                    LowSlot5.Visible = True
                Case 6
                    LowSlot6.Visible = True
                Case 7
                    LowSlot7.Visible = True
                Case 8
                    LowSlot8.Visible = True
            End Select
        Next
    End Sub

    Private Sub SetRigSlots(Slots As Integer)

        ' Init slots
        RigSlot1.Visible = False
        RigSlot2.Visible = False
        RigSlot3.Visible = False

        RigSlot1.Image = Nothing
        RigSlot2.Image = Nothing
        RigSlot3.Image = Nothing

        For i = 1 To Slots
            Select Case i
                Case 1
                    RigSlot1.Visible = True
                Case 2
                    RigSlot2.Visible = True
                Case 3
                    RigSlot3.Visible = True
            End Select
        Next
    End Sub

    Private Sub SetServiceSlots(Slots As Integer)

        ' Init slots
        ServiceSlot1.Visible = False
        ServiceSlot2.Visible = False
        ServiceSlot3.Visible = False
        ServiceSlot4.Visible = False
        ServiceSlot5.Visible = False
        ServiceSlot6.Visible = False

        ServiceSlot1.Image = Nothing
        ServiceSlot2.Image = Nothing
        ServiceSlot3.Image = Nothing
        ServiceSlot4.Image = Nothing
        ServiceSlot5.Image = Nothing
        ServiceSlot6.Image = Nothing

        For i = 1 To Slots
            Select Case i
                Case 1
                    ServiceSlot1.Visible = True
                Case 2
                    ServiceSlot2.Visible = True
                Case 3
                    ServiceSlot3.Visible = True
                Case 4
                    ServiceSlot4.Visible = True
                Case 5
                    ServiceSlot5.Visible = True
                Case 6
                    ServiceSlot6.Visible = True
            End Select
        Next

        If Slots Mod 2 > 0 And Slots < 6 Then
            ' Move the slots if we are on the first line
            Call ShiftServiceSlotImages()
        Else
            ' Reset them to the base positions
            Call ResetServiceSlotImages()
        End If

    End Sub

    Private Sub btnSaveUpdatePrices_Click(sender As Object, e As EventArgs) Handles btnSaveUpdatePrices.Click
        Dim SQL As String

        Try
            EVEDB.BeginSQLiteTransaction()
            ' Delete everything first, then insert the new records
            EVEDB.ExecuteNonQuerySQL(String.Format("DELETE FROM FACILITY_INSTALLED_MODULES WHERE CHARACTER_ID = {0} 
            AND INDUSTRY_TYPE = {1} AND FACILITY_VIEW = {2}", SelectedCharacterID, CStr(SelectedFacilityProductionType), CStr(SelectedStructureView)))

            ' Insert all the modules on the facility
            For Each InstalledModule In GetInstalledSlots()
                SQL = String.Format("INSERT INTO FACILITY_INSTALLED_MODULES VALUES({0},{1},{2},{3})",
                                    SelectedCharacterID, CStr(SelectedFacilityProductionType), CStr(SelectedStructureView), InstalledModule.typeID)
                EVEDB.ExecuteNonQuerySQL(SQL)
            Next

            MsgBox("Facility Saved", vbInformation, Application.ProductName)

            EVEDB.CommitSQLiteTransaction()

        Catch ex As Exception
            MsgBox("Facility failed to save: " & ex.Message, vbExclamation, Application.ProductName)
        End Try

    End Sub

    ' Loads up the bonuses from the modules installed in the list
    Private Sub UpdateUpwellStructureBonuses()
        Dim SQL As String
        Dim SystemSecurityBonus As Double
        Dim rsReader As SQLiteDataReader
        Dim DBCommand As SQLiteCommand

        Dim BonusList As ListViewItem

        lstUpwellStructureBonuses.Items.Clear()

        ' Loop through each module installed and get a total of all the stats affected and how
        For Each InstalledModule In GetInstalledSlots()
            ' Only look at rig bonuses for now
            If InstalledModule.moduleType.Contains("Rig") Then
                ' Get the security modifier first - set to 1 if not found
                SQL = "SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES WHERE typeID = {0} AND attributeID = "
                If chkHighSec.Checked Then
                    SQL &= CStr(ItemAttributes.hiSecModifier) & " "
                ElseIf chkLowSec.Checked Then
                    SQL &= CStr(ItemAttributes.lowSecModifier) & " "
                ElseIf chkNullSec.Checked Then
                    SQL &= CStr(ItemAttributes.nullSecModifier) & " "
                End If

                DBCommand = New SQLiteCommand(String.Format(SQL, InstalledModule.typeID), EVEDB.DBREf)
                rsReader = DBCommand.ExecuteReader

                If rsReader.Read Then
                    SystemSecurityBonus = rsReader.GetDouble(0)
                Else
                    SystemSecurityBonus = 1
                End If

                ' Engineering Rigs
                Select Case InstalledModule.moduleType
                    Case "EngineeringRigs"

                        SQL = "SELECT CASE WHEN groupName IS NULL THEN categoryName ELSE groupname END AS APPPLICATION, activityName, "
                        SQL &= "AT.displayName || ': ' || CAST(COALESCE(valueint, valuefloat)*100 AS VARCHAR) || '%' AS BONUSES, typeName AS BONUS_SOURCE "

                        SQL &= "(SELECT AT.DISPALYCOALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                        SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.attributeEngRigMatBonus) & " "
                        SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS ME_VALUE, "
                        SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                        SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.attributeEngRigTimeBonus) & " "
                        SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS TE_VALUE, "
                        SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                        SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.attributeEngRigCostBonus) & " "
                        SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS COST_VALUE, "

                    Case "CombatRigs"


                    Case "ReprocessingRigs"

                        SQL = "SELECT 'Refining' AS BONUS_APPLIES, 'Refining' AS ACTIVITY, "
                        SQL &= "AT.displayName || ': ' || CAST(COALESCE(valueint, valuefloat)*100 AS VARCHAR) || '%' AS BONUSES, typeName AS BONUS_SOURCE "
                        SQL &= "FROM TYPE_ATTRIBUTES AS TA, INVENTORY_TYPES AS IT, ATTRIBUTE_TYPES AS AT "
                        SQL &= "WHERE TA.attributeID = AT.attributeID "
                        SQL &= "AND TA.typeID = IT.typeID AND TA.attributeID IN (SELECT attributeID FROM ATTRIBUTE_TYPES WHERE attributeName LIKE 'refiningYield%') "
                        SQL &= SQL & "AND TA.typeID = {0} ORDER BY BONUSES "

                    Case "ReactionRigs"


                    Case "DrillingRigs"

                    Case Else
                        Exit For
                End Select

                ' Look up the stats for the item and the bonus based on the type of security in the system and te,mat,cost

                ' Engineering Rigs

                ' Refining Rigs

                ' Reaction Rigs
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.RefRigMatBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS REACTION_MAT_BONUS, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.RefRigTimeBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS REACTION_TIME_BONUS, "
                ' Drilling Rigs
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.moonRigFractureDelayBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Chunk_Stability_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.moonRigAsteroidDecayBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Extracted_Asteroid_Decay_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.moonRigSpewRadiusBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Moon_Asteroid_Belt_Radius_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.moonRigSpewVolumeBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Moon_Extraction_Volume_Bonus, "
                ' Combat Rigs
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigMissileExploVeloBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Explosion_Velocity_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigEwarOptimalBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Optimal_Range_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigEwarFalloffBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Falloff_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigMissileVelocityBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Missile_Velocity_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigEwarCapUseBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Capacitor_Use_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigMaxTargetBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Maximum_Locked_Targets_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigScanResBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Scan_Resolution_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigMissileExplosionRadiusBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Guided_Bomb_Explosion_Radius_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigPDRangeBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Point_Defense_Battery_Range_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigPDCapUseBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Point_Defense_Battery_Capacitor_Use_Bonus, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigDoomsdayTargetAmountBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Additional_doomsday_secondary_targets, "
                SQL &= "(SELECT COALESCE(valueint, valuefloat) FROM TYPE_ATTRIBUTES AS TA, ATTRIBUTE_TYPES AS AT "
                SQL &= "WHERE TA.attributeID = AT.attributeID AND AT.attributeID = " & CStr(ItemAttributes.structureRigDoomsdayDamageLossTargetBonus) & " "
                SQL &= "AND typeID = {0}) * COALESCE(valueint, valuefloat)/100 AS Bonus_to_doomsday_secondary_target_damage_reduction, "

                SQL &= "typeName, INVENTORY_CATEGORIES.categoryID, RAM_ACTIVITIES.activityID, AT.displayName AS BONUS "
                SQL &= "FROM TYPE_ATTRIBUTES AS TA, ENGINEERING_RIG_BONUSES AS SRB, INVENTORY_TYPES AS IT, ATTRIBUTE_TYPES AS AT "
                SQL &= "LEFT JOIN INVENTORY_GROUPS ON SRB.groupID = INVENTORY_GROUPS.groupID "
                SQL &= "LEFT JOIN INVENTORY_CATEGORIES ON SRB.categoryID = INVENTORY_CATEGORIES.categoryID "
                SQL &= "LEFT JOIN RAM_ACTIVITIES ON SRB.activityID = RAM_ACTIVITIES.activityID "
                SQL &= "WHERE SRB.typeID = TA.typeID AND SRB.typeID = IT.typeID AND TA.attributeID = AT.attributeID "
                SQL &= "AND SRB.typeID = {0} AND TA.attributeID = "
                If chkHighSec.Checked Then
                    SQL &= CStr(ItemAttributes.hiSecModifier) & " "
                ElseIf chkLowSec.Checked Then
                    SQL &= CStr(ItemAttributes.lowSecModifier) & " "
                ElseIf chkNullSec.Checked Then
                    SQL &= CStr(ItemAttributes.nullSecModifier) & " "
                End If
                SQL &= "AND ME_VALUE IS NOT NULL AND TE_VALUE IS NOT NULL AND COST_VALUE IS NOT NULL "
                SQL &= "ORDER BY APPPLICATION"

                DBCommand = New SQLiteCommand(String.Format(SQL, InstalledModule.typeID), EVEDB.DBREf)
                rsReader = DBCommand.ExecuteReader

                While rsReader.Read
                    ' Insert a row with the data pulled
                    ' Columns: Bonus Applies to, Activity, Bonuses, Bonus Source

                    BonusList = New ListViewItem(rsReader.GetString(0)) ' Group or Category bonus is applied

                    ' Set the activity name
                    If Not IsDBNull(rsReader.GetValue(6)) Then
                        If rsReader.GetInt32(6) = 9 Then
                            If rsReader.GetInt32(7) = -1 Then
                                ' All laboratory things
                                BonusList.SubItems.Add("All Laboratory Jobs")
                            Else
                                BonusList.SubItems.Add(CStr(rsReader.GetString(1))) ' Activity
                            End If
                        Else
                            ' Just the category
                            BonusList.SubItems.Add(CStr(rsReader.GetString(1))) ' Activity
                        End If
                    Else
                        ' Group name must have something then
                        BonusList.SubItems.Add(CStr(rsReader.GetString(1))) ' Activity
                    End If

                    BonusList.SubItems.Add(FormatPercent(rsReader.GetDouble(2), 3)) ' ME
                    BonusList.SubItems.Add(FormatPercent(rsReader.GetDouble(3), 3)) ' TE
                    BonusList.SubItems.Add(FormatPercent(rsReader.GetDouble(4), 3)) ' Cost

                    BonusList.SubItems.Add(CStr(rsReader.GetString(5))) ' Source of bonus

                    ' Update the final list
                    Call lstUpwellStructureBonuses.Items.Add(BonusList)

                End While
            End If
        Next

    End Sub


#Region "Fuel Settings"

    Private Sub btnSaveFuelBlockInfo_Click(sender As Object, e As EventArgs) Handles btnSaveFuelBlockInfo.Click

    End Sub

    Private Sub btnRefreshBlockData_Click(sender As Object, e As EventArgs) Handles btnRefreshBlockData.Click

    End Sub

    Private Sub btnUpdateBlockPrice_Click(sender As Object, e As EventArgs) Handles btnUpdatePrices.Click

    End Sub

    Private Sub LoadPOSDataTab()

        txtHeliumFuelBlockBPME.Text = "0"

        ' Building
        If SelectedTower.FuelBlockBuild Then
            rbtnBuildBlocks.Checked = True
            gbFuelPrices.Enabled = True
            btnUpdatePrices.Enabled = False
            btnUpdatePrices.Enabled = True
            btnRefreshBlockData.Enabled = True
            txtHeliumFuelBlockBPME.Enabled = True
            lblHeliumFuelBlockBPME.Enabled = True
            txtHeliumFuelBlockBuyPrice.Enabled = False
        Else ' Buying
            rbtnBuyBlocks.Checked = True
            gbFuelPrices.Enabled = False
            btnUpdatePrices.Enabled = True
            btnUpdatePrices.Enabled = False
            btnRefreshBlockData.Enabled = False
            txtHeliumFuelBlockBPME.Enabled = False
            lblHeliumFuelBlockBPME.Enabled = False
            txtHeliumFuelBlockBuyPrice.Enabled = True
        End If

        txtCharters.Text = FormatNumber(SelectedTower.CharterCost, 2)

        Call LoadPOSFuelPrices()
        ' Load both the block build and buy prices
        Call LoadPOSFuelBlockPrice()
        Call SetFuelBlockBuildcost()


    End Sub

    Private Sub UpdateFuelBlockData(ByVal TowerName As String, ReloadME As Boolean)
        Dim SQL As String
        Dim readerPOS As SQLiteDataReader
        Dim FuelBlock As String = ""
        Dim SelectedTowerRaceID As Integer

        SQL = "Select raceID FROM INVENTORY_TYPES WHERE typeName ='" & TowerName & "' "

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        readerPOS = DBCommand.ExecuteReader()

        If readerPOS.Read Then
            SelectedTowerRaceID = readerPOS.GetInt32(0)
        Else
            MsgBox("Unknown Tower. Cannot calculate.", vbExclamation, Application.ProductName)
            Exit Sub
        End If

        picHeliumFuelBlock.Visible = False
        picHydrogenFuelBlock.Visible = False
        picNitrogenFuelBlock.Visible = False
        picOxygenFuelBlock.Visible = False

        ' Based on the race of the tower, choose the type of fuel block it will use
        Select Case SelectedTowerRaceID
            Case 1
                FuelBlock = "Caldari Fuel Block"
                picHydrogenFuelBlock.Visible = True
            Case 2
                FuelBlock = "Minmatar Fuel Block"
                picOxygenFuelBlock.Visible = True
            Case 4
                FuelBlock = "Amarr Fuel Block"
                picHeliumFuelBlock.Visible = True
            Case 8
                FuelBlock = "Gallente Fuel Block"
                picNitrogenFuelBlock.Visible = True
        End Select

        ' Reload the ME if we need too
        If ReloadME Then
            Call LoadBlockBPME(FuelBlock)
        End If

        ' Build the block value if we are building
        If rbtnBuildBlocks.Checked Then
            Call SetFuelBlockBuildcost()
        End If

    End Sub

    Private Sub LoadBlockBPME(FuelBlockName As String)
        ' Load the ME for the type of block that we are using for this tower
        Dim SQL As String
        Dim readerPOS As SQLiteDataReader

        SQL = "SELECT ME FROM OWNED_BLUEPRINTS, ALL_BLUEPRINTS "
        SQL = SQL & "WHERE ALL_BLUEPRINTS.BLUEPRINT_ID = OWNED_BLUEPRINTS.BLUEPRINT_ID "

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        readerPOS = DBCommand.ExecuteReader()

        If readerPOS.Read Then
            ' Owned and they have it
            txtHeliumFuelBlockBPME.Text = CStr(readerPOS.GetValue(0))
        Else
            txtHeliumFuelBlockBPME.Text = "0"
        End If

    End Sub

    Private Sub UpdatePOSFuelPrices()
        'Dim SQL As String
        'Dim i As Integer
        'Dim Prices() As Double

        If POSFuelPricesUpdated Then
            'Me.Cursor = Cursors.WaitCursor

            'ReDim Prices(POSTextBoxes.Count - 1)

            '' Check the prices first
            'For i = 1 To POSTextBoxes.Count - 1
            '    If Not IsNumeric(POSTextBoxes(i).Text) Then
            '        MsgBox("Invalid " & POSLabels(i).Text & " Price", vbExclamation, Me.Text)
            '        POSTextBoxes(i).Focus()
            '        Me.Cursor = Cursors.Default
            '        Exit Sub
            '    Else
            '        Prices(i) = CDbl(POSTextBoxes(i).Text)
            '    End If
            'Next

            '' Update all the prices
            'For i = 1 To POSTextBoxes.Count - 1
            '    SQL = "UPDATE ITEM_PRICES SET PRICE = " & Prices(i) & ", PRICE_TYPE = 'User' WHERE ITEM_NAME = '" & POSLabels(i).Text & "'"
            '    Call EVEDB.ExecuteNonQuerySQL(SQL)
            'Next

            'MsgBox("Prices Updated", vbInformation, Me.Text)
            'Me.Cursor = Cursors.Default

            '' Update the block data
            Call SetFuelBlockBuildcost()
        Else
            MsgBox("No Prices were Updated", vbInformation, Me.Text)
        End If

        ' Refresh the prices
        Call LoadPOSFuelPrices()

    End Sub

    Private Sub LoadPOSFuelPrices()
        Dim SQL As String
        Dim readerPOS As SQLiteDataReader

        Me.Cursor = Cursors.WaitCursor

        SQL = "SELECT ITEM_PRICES.ITEM_NAME, ITEM_PRICES.PRICE "
        SQL = SQL & "FROM ITEM_PRICES "
        SQL = SQL & "WHERE ITEM_PRICES.ITEM_NAME IN "
        SQL = SQL & "('Hydrogen Isotopes','Oxygen Isotopes','Nitrogen Isotopes','Helium Isotopes','Strontium Clathrates',"
        SQL = SQL & "'Heavy Water','Liquid Ozone','Robotics','Oxygen','Mechanical Parts','Coolant','Enriched Uranium')"

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        readerPOS = DBCommand.ExecuteReader()

        While readerPOS.Read
            ' Update the textboxes with prices
            Select Case readerPOS.GetString(0)
                Case "Hydrogen Isotopes"
                    txtHeliumIsotopes.Text = FormatNumber(readerPOS.GetDouble(1), 2)


            End Select
            Application.DoEvents()
        End While

        Me.Cursor = Cursors.Default
        txtHeliumIsotopes.Focus()

        readerPOS.Close()
        readerPOS = Nothing
        DBCommand = Nothing

        POSFuelPricesUpdated = False

    End Sub

    Private Sub UpdatePOSFuelBlockPrices()
        Dim SQL As String
        Dim posfuelblockpricesupdated As Boolean

        If posfuelblockpricesupdated Then
            Me.Cursor = Cursors.WaitCursor

            ' Check the prices first

            If Not IsNumeric(txtHeliumFuelBlockBuyPrice.Text) Then
                MsgBox("Invalid Fuel Block Price", vbExclamation, Application.ProductName)
                txtHeliumFuelBlockBuyPrice.Focus()
                Me.Cursor = Cursors.Default
                Exit Sub
            End If

            ' Update the prices
            SQL = "UPDATE ITEM_PRICES SET PRICE = " & CDec(txtHeliumFuelBlockBuyPrice.Text) & ", PRICE_TYPE = 'User' WHERE ITEM_NAME = '" & lblHeliumFuelBlock.Text & " Fuel Block'"
            Call EVEDB.ExecuteNonQuerySQL(SQL)

            MsgBox("Prices Updated", vbInformation, Me.Text)
            Me.Cursor = Cursors.Default
        Else
            MsgBox("No Prices were Updated", vbInformation, Me.Text)
        End If

        ' Refresh the prices
        Call LoadPOSFuelBlockPrice()

    End Sub

    Private Sub LoadPOSFuelBlockPrice()
        Dim SQL As String
        Dim readerPOS As SQLiteDataReader
        Dim selectedtowerraceid As Integer

        Me.Cursor = Cursors.WaitCursor

        readerPOS = Nothing
        DBCommand = Nothing

        If cmbUpwellStructureName.Text <> None Then
            ' Load the fuel block price
            SQL = "SELECT ITEM_PRICES.ITEM_NAME, ITEM_PRICES.PRICE "
            SQL = SQL & "FROM ITEM_PRICES, INVENTORY_TYPES "
            SQL = SQL & "WHERE ITEM_PRICES.ITEM_NAME = "
            Select Case selectedtowerraceid
                Case 1
                    SQL = SQL & "'Caldari Fuel Block' "
                Case 2
                    SQL = SQL & "'Minmatar Fuel Block' "
                Case 4
                    SQL = SQL & "'Amarr Fuel Block' "
                Case 8
                    SQL = SQL & "'Gallente Fuel Block' "
            End Select
            SQL = SQL & "AND INVENTORY_TYPES.typeID = ITEM_PRICES.ITEM_ID "

            DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
            readerPOS = DBCommand.ExecuteReader()
            readerPOS.Read()

            txtHeliumFuelBlockBuyPrice.Text = FormatNumber(readerPOS.GetValue(1))
            readerPOS.Close()
        Else
            txtHeliumFuelBlockBuyPrice.Text = "0.00"
        End If

        Me.Cursor = Cursors.Default

        readerPOS = Nothing
        DBCommand = Nothing

    End Sub

    Private Sub UpdateCosts()
        Dim CostperBlock As Double
        Dim CostperHour As Double
        Dim Multiplier As Integer

        ' Get the block we are using
        If rbtnBuildBlocks.Checked Then
            CostperBlock = CDbl(lblHeliumFuelBlockBuild.Text)
        Else
            CostperBlock = CDbl(txtHeliumFuelBlockBuyPrice.Text)
        End If

        CostperHour = CostperBlock * Multiplier
        'lblPOSCostperHour.Text = FormatNumber(CostperHour, 2)
        'lblPOSCostperDay.Text = FormatNumber(CostperHour * 24, 2)
        'lblPOSCostperMonth.Text = FormatNumber(CostperHour * 24 * 30, 2)

    End Sub

    Private Sub SetFuelBlockBuildcost()

        ' Make sure it's valid
        If Not IsNumeric(txtHeliumFuelBlockBPME.Text) Then
            MsgBox("Invalid Fuel Block BPO ME", vbExclamation, Application.ProductName)
            txtHeliumFuelBlockBPME.Focus()
            Exit Sub
        End If

        If Trim(txtCharters.Text) = "" Or Not IsNumeric(txtCharters.Text) Then
            MsgBox("Invalid Charter Cost", vbExclamation, Application.ProductName)
            txtCharters.Focus()
            Exit Sub
        End If

        ' First set all to 0 so we only build for the tower we are using
        lblHeliumFuelBlockBuild.Text = "0.00"

        ' Get cost for building 1 block and add charter cost (divide by 40 since that's the number of blocks that is made for 1 charter)
        If rbtnHydrogenFuelBlock.Checked Then
            lblHeliumFuelBlockBuild.Text = FormatNumber(GetFuelBlockBuildCost("Caldari Fuel Block", CInt(txtHeliumFuelBlockBPME.Text)) + (CInt(txtCharters.Text) / 40), 2)
        ElseIf rbtnOxygenFuelBlock.Checked Then
            lblHeliumFuelBlockBuild.Text = FormatNumber(GetFuelBlockBuildCost("Minmatar Fuel Block", CInt(txtHeliumFuelBlockBPME.Text)) + (CInt(txtCharters.Text) / 40), 2)
        ElseIf rbtnHeliumFuelBlock.Checked Then
            lblHeliumFuelBlockBuild.Text = FormatNumber(GetFuelBlockBuildCost("Amarr Fuel Block", CInt(txtHeliumFuelBlockBPME.Text)) + (CInt(txtCharters.Text) / 40), 2)
        ElseIf rbtnNitrogenFuelBlock.Checked Then
            lblHeliumFuelBlockBuild.Text = FormatNumber(GetFuelBlockBuildCost("Gallente Fuel Block", CInt(txtHeliumFuelBlockBPME.Text)) + (CInt(txtCharters.Text) / 40), 2)
        End If

    End Sub

    Private Function GetFuelBlockBuildCost(FuelBlock As String, bpME As Integer) As Double
        Dim SQL As String
        Dim readerPOS As SQLiteDataReader


        SQL = "SELECT BLUEPRINT_ID FROM ALL_BLUEPRINTS WHERE ITEM_NAME = '" & FuelBlock & "'"

        DBCommand = New SQLiteCommand(SQL, EVEDB.DBREf)
        readerPOS = DBCommand.ExecuteReader()

        If readerPOS.Read Then
            ' Build T1 BP for the block, standard settings - CHECK
            ' Dim BlockBP = New Blueprint(readerPOS.GetInt64(0), 1, bpME, 0, 1, 1, SelectedCharacter, UserApplicationSettings, False, 0, NoTeam,
            'SelectedBPManufacturingFacility, NoTeam, SelectedBPComponentManufacturingFacility, SelectedBPCapitalComponentManufacturingFacility)
            '  Call BlockBP.BuildItems(False, False, False, False, False)
            ' Return BlockBP.GetRawItemUnitPrice
            Return 0
        Else
            Return 0
        End If

    End Function

#End Region

#Region "Click Events"
    Private Sub cmbUpwellStructureName_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbUpwellStructureName.SelectedIndexChanged
        Call LoadStructure(cmbUpwellStructureName.Text)
    End Sub

    Private Sub chkItemViewTypeAll_CheckedChanged(sender As Object, e As EventArgs)
        Call UpdateFittingImages()
    End Sub

    Private Sub chkItemViewTypeHigh_CheckedChanged(sender As Object, e As EventArgs) Handles chkItemViewTypeHigh.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkItemViewTypeLow_CheckedChanged(sender As Object, e As EventArgs) Handles chkItemViewTypeLow.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkItemViewTypeMedium_CheckedChanged(sender As Object, e As EventArgs) Handles chkItemViewTypeMedium.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkItemViewTypeServices_CheckedChanged(sender As Object, e As EventArgs) Handles chkItemViewTypeServices.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkRigTypeViewCombat_CheckedChanged(sender As Object, e As EventArgs) Handles chkRigTypeViewCombat.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkRigTypeViewReaction_CheckedChanged(sender As Object, e As EventArgs) Handles chkRigTypeViewReaction.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkRigViewTypeDrilling_CheckedChanged(sender As Object, e As EventArgs) Handles chkRigTypeViewDrilling.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkRigTypeViewEngineering_CheckedChanged(sender As Object, e As EventArgs) Handles chkRigTypeViewEngineering.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub chkRigTypeViewReprocessing_CheckedChanged(sender As Object, e As EventArgs) Handles chkRigTypeViewReprocessing.CheckedChanged
        Call UpdateFittingImages()
    End Sub

    Private Sub cmbUpwellStructureName_KeyPress(sender As Object, e As KeyPressEventArgs) Handles cmbUpwellStructureName.KeyPress
        e.Handled = True
    End Sub

    Private Sub MidSlot1_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot1.DoubleClick
        MidSlot1.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub MidSlot2_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot2.DoubleClick
        MidSlot2.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub MidSlot3_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot3.DoubleClick
        MidSlot3.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub MidSlot4_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot4.DoubleClick
        MidSlot4.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub MidSlot5_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot5.DoubleClick
        MidSlot5.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub MidSlot6_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot6.DoubleClick
        MidSlot6.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub MidSlot7_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot7.DoubleClick
        MidSlot7.Image = Nothing
    End Sub

    Private Sub MidSlot8_DoubleClick(sender As Object, e As EventArgs) Handles MidSlot8.DoubleClick
        MidSlot8.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot1_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot1.DoubleClick
        If Not IsNothing(HighSlot1.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot1.Image.Tag))
        End If
        HighSlot1.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot2_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot2.DoubleClick
        If Not IsNothing(HighSlot2.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot2.Image.Tag))
        End If
        HighSlot2.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot3_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot3.DoubleClick
        If Not IsNothing(HighSlot3.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot3.Image.Tag))
        End If
        HighSlot3.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot5_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot5.DoubleClick
        If Not IsNothing(HighSlot5.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot5.Image.Tag))
        End If
        HighSlot5.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot7_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot7.DoubleClick
        If Not IsNothing(HighSlot7.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot7.Image.Tag))
        End If
        HighSlot7.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot4_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot4.DoubleClick
        If Not IsNothing(HighSlot4.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot4.Image.Tag))
        End If
        HighSlot4.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot6_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot6.DoubleClick
        If Not IsNothing(HighSlot6.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot6.Image.Tag))
        End If
        HighSlot6.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub HighSlot8_DoubleClick(sender As Object, e As EventArgs) Handles HighSlot8.DoubleClick
        If Not IsNothing(HighSlot8.Image) Then
            Call UpdateLauncherSlots(True, CStr(HighSlot8.Image.Tag))
        End If
        HighSlot8.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub RigSlot3_DoubleClick(sender As Object, e As EventArgs) Handles RigSlot3.DoubleClick
        RigSlot3.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub RigSlot2_DoubleClick(sender As Object, e As EventArgs) Handles RigSlot2.DoubleClick
        RigSlot2.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub RigSlot1_DoubleClick(sender As Object, e As EventArgs) Handles RigSlot1.DoubleClick
        RigSlot1.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot1_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot1.DoubleClick
        LowSlot1.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot2_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot2.DoubleClick
        LowSlot2.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot3_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot3.DoubleClick
        LowSlot3.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot4_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot4.DoubleClick
        LowSlot4.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot5_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot5.DoubleClick
        LowSlot5.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot6_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot6.DoubleClick
        LowSlot6.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot7_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot7.DoubleClick
        LowSlot7.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub LowSlot8_DoubleClick(sender As Object, e As EventArgs) Handles LowSlot8.DoubleClick
        LowSlot8.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub ServiceSlot5_DoubleClick(sender As Object, e As EventArgs) Handles ServiceSlot5.DoubleClick
        ServiceSlot5.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub ServiceSlot3_DoubleClick(sender As Object, e As EventArgs) Handles ServiceSlot3.DoubleClick
        ServiceSlot3.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub ServiceSlot1_DoubleClick(sender As Object, e As EventArgs) Handles ServiceSlot1.DoubleClick
        ServiceSlot1.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub ServiceSlot2_DoubleClick(sender As Object, e As EventArgs) Handles ServiceSlot2.DoubleClick
        ServiceSlot2.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub ServiceSlot4_DoubleClick(sender As Object, e As EventArgs) Handles ServiceSlot4.DoubleClick
        ServiceSlot4.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub ServiceSlot6_DoubleClick(sender As Object, e As EventArgs) Handles ServiceSlot6.DoubleClick
        ServiceSlot6.Image = Nothing
        Call UpdateUpwellStructureStats()
    End Sub

    Private Sub btnToggleAllPriceItems_Click(sender As Object, e As EventArgs) Handles btnToggleAllPriceItems.Click
        Call StripFitting()
    End Sub

    Private Sub chkIncludeFuelCosts_CheckedChanged(sender As Object, e As EventArgs) Handles chkIncludeFuelCosts.CheckedChanged
        Call UpdateFuelCostLabels()
    End Sub

    Private Sub btnItemFilter_Click(sender As Object, e As EventArgs) Handles btnItemFilter.Click
        Call UpdateFittingImages()
    End Sub

    Private Sub btnResetItemFilter_Click(sender As Object, e As EventArgs) Handles btnResetItemFilter.Click
        txtItemFilter.Text = ""
        Call UpdateFittingImages()
    End Sub

    Private Sub txtItemFilter_KeyDown(sender As Object, e As KeyEventArgs) Handles txtItemFilter.KeyDown
        If e.KeyCode = Keys.Enter Then
            Call UpdateFittingImages()
        End If
    End Sub

    Private Sub ServiceModuleListView_ItemActivate(sender As Object, e As EventArgs) Handles ServiceModuleListView.ItemActivate
        Call LoadImageInFreeSlot()
    End Sub

    Private Sub btnCloseForm_Click(sender As Object, e As EventArgs) Handles btnCloseForm.Click
        Me.Hide()
    End Sub

    Private Sub chkHighSec_CheckedChanged(sender As Object, e As EventArgs) Handles chkHighSec.CheckedChanged
        If Not UpdateChecks Then
            Call SetSpaceSecurityChecks(0)
            Call UpdateUpwellStructureBonuses()
        End If
    End Sub

    Private Sub chkLowSec_CheckedChanged(sender As Object, e As EventArgs) Handles chkLowSec.CheckedChanged
        If Not UpdateChecks Then
            Call SetSpaceSecurityChecks(1)
            Call UpdateUpwellStructureBonuses()
        End If
    End Sub

    Private Sub chkNullSec_CheckedChanged(sender As Object, e As EventArgs) Handles chkNullSec.CheckedChanged
        If Not UpdateChecks Then
            Call SetSpaceSecurityChecks(2)
            Call UpdateUpwellStructureBonuses()
        End If
    End Sub

    ' Ensures one is at least checked
    Private Sub SetSpaceSecurityChecks(ByVal TriggerIndex As Integer)
        Dim i As Integer

        If Not FirstLoad Then
            ' Adjust the checks depending on options
            For i = 0 To SecurityCheckBoxes.Count - 1
                UpdateChecks = True
                If i <> TriggerIndex Then
                    SecurityCheckBoxes(i).Checked = False
                ElseIf i = TriggerIndex And SecurityCheckBoxes(i).Checked = False Then
                    SecurityCheckBoxes(i).Checked = True ' Don't let them uncheck the value
                End If
                UpdateChecks = False
            Next
        End If
        'End If
    End Sub

    Private Sub btnSaveSettings_Click(sender As Object, e As EventArgs) Handles btnSaveSettings.Click
        Dim TempSettings As UpwellStructureSettings = Nothing
        Dim Settings As New ProgramSettings


        With TempSettings
            .HighSlotsCheck = chkItemViewTypeHigh.Checked
            .MediumSlotsCheck = chkItemViewTypeMedium.Checked
            .LowSlotsCheck = chkItemViewTypeLow.Checked
            .ServicesCheck = chkItemViewTypeServices.Checked

            .ReprocessingRigsCheck = chkRigTypeViewReprocessing.Checked
            .EngineeringRigsCheck = chkRigTypeViewEngineering.Checked
            .CombatRigsCheck = chkRigTypeViewCombat.Checked
            .ReactionsRigsCheck = chkRigTypeViewReaction.Checked
            .DrillingRigsCheck = chkRigTypeViewDrilling.Checked

            .SearchFilterText = txtItemFilter.Text

            .IncludeFuelCostsCheck = chkIncludeFuelCosts.Checked

            If rbtnHeliumFuelBlock.Checked Then
                .FuelBlockType = rbtnHeliumFuelBlock.Text
            ElseIf rbtnHydrogenFuelBlock.Checked Then
                .FuelBlockType = rbtnHydrogenFuelBlock.Text
            ElseIf rbtnNitrogenFuelBlock.Checked Then
                .FuelBlockType = rbtnNitrogenFuelBlock.Text
            ElseIf rbtnOxygenFuelBlock.Checked Then
                .FuelBlockType = rbtnOxygenFuelBlock.Text
            End If

            If rbtnBuildBlocks.Checked Then
                .BuyBuildBlockOption = rbtnBuildBlocks.Text
            ElseIf rbtnBuyBlocks.Checked Then
                .BuyBuildBlockOption = rbtnBuyBlocks.Text
            End If

            .SelectedStructureName = cmbUpwellStructureName.Text

        End With

        ' Save the data in the XML file
        Call Settings.SaveUpwellStructureViewerSettings(TempSettings)

        ' Save the data to the local variable
        UserUpwellStructureSettings = TempSettings

        MsgBox("Settings Saved", vbInformation, Application.ProductName)

    End Sub


#End Region

End Class