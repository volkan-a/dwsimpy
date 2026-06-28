'    Sensitivity Analysis Classes
'    Copyright 2009-2014 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

Imports System.Xml

Namespace Flowsheet.Optimization

    <System.Serializable()> Public Class SensitivityAnalysisCase

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Public iv1 As New SAVariable
        Public iv2 As New SAVariable
        Public dv As New SAVariable

        Public name As String = ""
        Public description As String = ""
        <Xml.Serialization.XmlIgnore()> Public results As ArrayList
        Public stats As String = ""
        Public numvar As Integer = 1

        Public depvartype As SADependentVariableType = SADependentVariableType.Variable

        Public expression As String = ""

        'Expression compilation belongs to the solver layer, not this data assembly.
        <System.NonSerialized()> Public exbase As Object
        <System.NonSerialized()> Public econtext As Object

        Public variables As New Dictionary(Of String, SAVariable)
        Public depvariables As New Dictionary(Of String, SAVariable)

        Public Sub New()
            iv1 = New SAVariable
            iv2 = New SAVariable
            dv = New SAVariable
            variables = New Dictionary(Of String, SAVariable)
            depvariables = New Dictionary(Of String, SAVariable)
            results = New ArrayList()
        End Sub

        Public Function Clone() As Object Implements ICloneable.Clone

            Dim copy As New SensitivityAnalysisCase With {
                .iv1 = DirectCast(iv1.Clone(), SAVariable),
                .iv2 = DirectCast(iv2.Clone(), SAVariable),
                .dv = DirectCast(dv.Clone(), SAVariable),
                .name = name,
                .description = description,
                .stats = stats,
                .numvar = numvar,
                .depvartype = depvartype,
                .expression = expression,
                .results = CloneResults(results)
            }

            For Each item In variables
                copy.variables(item.Key) = DirectCast(item.Value.Clone(), SAVariable)
            Next

            For Each item In depvariables
                copy.depvariables(item.Key) = DirectCast(item.Value.Clone(), SAVariable)
            Next

            Return copy

        End Function

        Public Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)

            iv1 = LoadVariable(data, "IV1")
            iv2 = LoadVariable(data, "IV2")
            dv = LoadVariable(data, "DV")

            LoadVariables(data, "Variables", variables)
            LoadVariables(data, "DepVariables", depvariables)
            Return True

        End Function

        Public Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Dim elements = XMLSerializer.XMLSerializer.Serialize(Me, True)
            elements.Add(New XElement("IV1", iv1.SaveData().ToArray()))
            elements.Add(New XElement("IV2", iv2.SaveData().ToArray()))
            elements.Add(New XElement("DV", dv.SaveData().ToArray()))
            elements.Add(SaveVariables("Variables", variables))
            elements.Add(SaveVariables("DepVariables", depvariables))
            Return elements

        End Function

        Private Shared Function LoadVariable(data As List(Of XElement), elementName As String) As SAVariable

            Dim variable As New SAVariable
            Dim element = data.FirstOrDefault(Function(item) item.Name.LocalName = elementName)
            If element IsNot Nothing Then variable.LoadData(element.Elements().ToList())
            Return variable

        End Function

        Private Shared Sub LoadVariables(data As List(Of XElement),
                                         elementName As String,
                                         destination As Dictionary(Of String, SAVariable))

            destination.Clear()
            Dim container = data.FirstOrDefault(Function(item) item.Name.LocalName = elementName)
            If container Is Nothing Then Return

            For Each variableElement In container.Elements()
                Dim variable As New SAVariable
                variable.LoadData(variableElement.Elements().ToList())
                Dim key = CStr(variableElement.Attribute("Key"))
                If Not String.IsNullOrWhiteSpace(key) Then destination(key) = variable
            Next

        End Sub

        Private Shared Function SaveVariables(elementName As String,
                                              source As Dictionary(Of String, SAVariable)) As XElement

            Dim container As New XElement(elementName)
            For Each item In source
                container.Add(New XElement("Variable",
                                           New XAttribute("Key", item.Key),
                                           item.Value.SaveData().ToArray()))
            Next
            Return container

        End Function

        Private Shared Function CloneResults(source As ArrayList) As ArrayList

            Dim copy As New ArrayList()
            If source Is Nothing Then Return copy

            For Each item In source
                If TypeOf item Is ICloneable Then
                    copy.Add(DirectCast(item, ICloneable).Clone())
                Else
                    copy.Add(item)
                End If
            Next

            Return copy

        End Function

    End Class

    <System.Serializable()> Public Class SAVariable

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Public objectID As String = ""
        Public objectTAG As String = ""
        Public propID As String = ""
        Public unit As String = ""
        Public points As Integer = 5
        Public name As String = ""
        Public id As String = ""
        Public currentvalue As Double = 0.0
        Public lowerlimit As Nullable(Of Double)
        Public upperlimit As Nullable(Of Double)

        Public Function Clone() As Object Implements ICloneable.Clone
            Return MemberwiseClone()
        End Function

        Public Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData
            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)
            Return True
        End Function

        Public Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData
            Return XMLSerializer.XMLSerializer.Serialize(Me, True)
        End Function

    End Class

    Public Enum SADependentVariableType
        Variable = 0
        Expression = 1
    End Enum

End Namespace
