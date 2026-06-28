'    Optimization Classes
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

    <System.Serializable()> Public Class OptimizationCase

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Public name As String = ""
        Public description As String = ""
        <Xml.Serialization.XmlIgnore()> Public results As ArrayList
        Public stats As String = ""

        Public solvm As SolvingMethod = SolvingMethod.AL_BRENT
        Public maxits As Integer = 100
        Public tolerance As Double = 0.000001
        Public epsX As Double = 0.001
        Public epsF As Double = 0.001
        Public epsG As Double = 0.001
        Public epsilon As Double = 0.001
        Public barriermultiplier As Double = 0.0001
        Public numdevscheme As Integer = 2

        Public boundvariables As Boolean = False

        Public objfunctype As OPTObjectiveFunctionType = OPTObjectiveFunctionType.Variable
        Public type As OPTType = OPTType.Minimization

        Public expression As String = ""

        'Expression compilation belongs to the solver layer, not this data assembly.
        <System.NonSerialized()> Public exbase As Object
        <System.NonSerialized()> Public econtext As Object

        Public variables As New Dictionary(Of String, OPTVariable)

        Public Enum SolvingMethod
            AL_BRENT = 0
            AL_BRENT_B = 1
            AL_LBFGS = 2
            AL_LBFGS_B = 3
            DN_TRUNCATED_NEWTON = 4
            DN_NELDERMEAD_SIMPLEX = 5
            DN_LBFGS = 6
            DN_TRUNCATED_NEWTON_B = 7
            DN_NELDERMEAD_SIMPLEX_B = 8
            DN_LBFGS_B = 9
            IPOPT = 10
        End Enum

        Public Sub New()
            variables = New Dictionary(Of String, OPTVariable)
            results = New ArrayList()
        End Sub

        Public Function Clone() As Object Implements ICloneable.Clone

            Dim copy As New OptimizationCase With {
                .name = name,
                .description = description,
                .stats = stats,
                .solvm = solvm,
                .maxits = maxits,
                .tolerance = tolerance,
                .epsX = epsX,
                .epsF = epsF,
                .epsG = epsG,
                .epsilon = epsilon,
                .barriermultiplier = barriermultiplier,
                .numdevscheme = numdevscheme,
                .boundvariables = boundvariables,
                .objfunctype = objfunctype,
                .type = type,
                .expression = expression,
                .results = CloneResults(results)
            }

            For Each item In variables
                copy.variables(item.Key) = DirectCast(item.Value.Clone(), OPTVariable)
            Next

            Return copy

        End Function

        Public Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)
            variables.Clear()

            Dim variablesElement = data.FirstOrDefault(Function(element) element.Name.LocalName = "Variables")
            If variablesElement IsNot Nothing Then
                For Each variableElement In variablesElement.Elements()
                    Dim variable As New OPTVariable
                    variable.LoadData(variableElement.Elements().ToList())
                    Dim key = CStr(variableElement.Attribute("Key"))
                    If Not String.IsNullOrWhiteSpace(key) Then variables(key) = variable
                Next
            End If

            Return True

        End Function

        Public Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Dim elements = XMLSerializer.XMLSerializer.Serialize(Me, True)
            Dim variablesElement As New XElement("Variables")

            For Each item In variables
                variablesElement.Add(New XElement("Variable",
                                                  New XAttribute("Key", item.Key),
                                                  item.Value.SaveData().ToArray()))
            Next

            elements.Add(variablesElement)
            Return elements

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

    <System.Serializable()> Public Class OPTVariable

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Public objectID As String = ""
        Public objectTAG As String = ""
        Public propID As String = ""
        Public unit As String = ""
        Public name As String = ""
        Public id As String = ""
        Public lowerlimit As Nullable(Of Double)
        Public upperlimit As Nullable(Of Double)
        Public currentvalue As Double = 0.0
        Public initialvalue As Double = 0.0
        Public type As OPTVariableType = OPTVariableType.Independent
        Public boundtype As BoundType = BoundType.None

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

    Public Enum OPTVariableType
        Dependent = 0
        Independent = 1
        Auxiliary = 2
        Constraint = 3
    End Enum

    Public Enum OPTObjectiveFunctionType
        Variable = 0
        Expression = 1
    End Enum

    Public Enum OPTType
        Minimization = 0
        Maximization = 1
    End Enum

    Public Enum BoundType
        None = 0
        Lower = 1
        Upper = 3
        LowerAndUpper = 2
    End Enum

End Namespace
