'    Petroleum Assay Class
'    Copyright 2012 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

Namespace Utilities.PetroleumCharacterization.Assay

    <System.Serializable()> Public Class Assay

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Private _name As String = ""
        Private _isbulk As Boolean
        Private _iscurve As Boolean

        Private _mw As Double
        Private _sg60 As Double
        Private _nbpavg As Double
        Private _t1 As Double
        Private _t2 As Double
        Private _v1 As Double
        Private _v2 As Double

        Private _nbptype As Integer
        Private _hasmwcurve As Boolean
        Private _hassgcurve As Boolean
        Private _sgcurvetype As String = "SG20"
        Private _hasvisccurves As Boolean
        Private _curvebasis As String = ""
        Private _api As Double
        Private _k_api As Double
        Private _px As ArrayList
        Private _py_nbp As ArrayList
        Private _py_mw As ArrayList
        Private _py_sg As ArrayList
        Private _py_v1 As ArrayList
        Private _py_v2 As ArrayList

        Public Sub New()
            _px = New ArrayList()
            _py_nbp = New ArrayList()
            _py_mw = New ArrayList()
            _py_sg = New ArrayList()
            _py_v1 = New ArrayList()
            _py_v2 = New ArrayList()
        End Sub

        Public Sub New(kApi As Double,
                       mw As Double,
                       api As Double,
                       t1 As Double,
                       t2 As Double,
                       nbpType As Integer,
                       sgType As String,
                       px As ArrayList,
                       pyNbp As ArrayList,
                       pyMw As ArrayList,
                       pySg As ArrayList,
                       pyV1 As ArrayList,
                       pyV2 As ArrayList)

            Me.New()
            _k_api = kApi
            _mw = mw
            _api = api
            _t1 = t1
            _t2 = t2
            _nbptype = nbpType
            _sgcurvetype = sgType
            _px = px
            _py_nbp = pyNbp
            _hasmwcurve = pyMw.Count > 0
            _py_mw = pyMw
            _hassgcurve = pySg.Count > 0
            _py_sg = pySg
            _hasvisccurves = pyV1.Count > 0
            _py_v1 = pyV1
            _py_v2 = pyV2
            _iscurve = True

        End Sub

        Public Sub New(mw As Double,
                       sg60 As Double,
                       nbpAverage As Double,
                       t1 As Double,
                       t2 As Double,
                       v1 As Double,
                       v2 As Double)

            Me.New()
            _mw = mw
            _sg60 = sg60
            _nbpavg = nbpAverage
            _t1 = t1
            _t2 = t2
            _v1 = v1
            _v2 = v2
            _isbulk = True

        End Sub

        Public Property CurveBasis As String
            Get
                Return _curvebasis
            End Get
            Set(value As String)
                _curvebasis = value
            End Set
        End Property

        Public Property PY_V2 As ArrayList
            Get
                Return _py_v2
            End Get
            Set(value As ArrayList)
                _py_v2 = value
            End Set
        End Property

        Public Property PY_V1 As ArrayList
            Get
                Return _py_v1
            End Get
            Set(value As ArrayList)
                _py_v1 = value
            End Set
        End Property

        Public Property PY_SG As ArrayList
            Get
                Return _py_sg
            End Get
            Set(value As ArrayList)
                _py_sg = value
            End Set
        End Property

        Public Property PY_MW As ArrayList
            Get
                Return _py_mw
            End Get
            Set(value As ArrayList)
                _py_mw = value
            End Set
        End Property

        Public Property PY_NBP As ArrayList
            Get
                Return _py_nbp
            End Get
            Set(value As ArrayList)
                _py_nbp = value
            End Set
        End Property

        Public Property PX As ArrayList
            Get
                Return _px
            End Get
            Set(value As ArrayList)
                _px = value
            End Set
        End Property

        Public Property K_API As Double
            Get
                Return _k_api
            End Get
            Set(value As Double)
                _k_api = value
            End Set
        End Property

        Public Property API As Double
            Get
                Return _api
            End Get
            Set(value As Double)
                _api = value
            End Set
        End Property

        Public Property SGCurveType As String
            Get
                Return _sgcurvetype
            End Get
            Set(value As String)
                _sgcurvetype = value
            End Set
        End Property

        Public Property HasViscCurves As Boolean
            Get
                Return _hasvisccurves
            End Get
            Set(value As Boolean)
                _hasvisccurves = value
            End Set
        End Property

        Public Property HasSGCurve As Boolean
            Get
                Return _hassgcurve
            End Get
            Set(value As Boolean)
                _hassgcurve = value
            End Set
        End Property

        Public Property HasMWCurve As Boolean
            Get
                Return _hasmwcurve
            End Get
            Set(value As Boolean)
                _hasmwcurve = value
            End Set
        End Property

        Public Property NBPType As Integer
            Get
                Return _nbptype
            End Get
            Set(value As Integer)
                _nbptype = value
            End Set
        End Property

        Public Property V2 As Double
            Get
                Return _v2
            End Get
            Set(value As Double)
                _v2 = value
            End Set
        End Property

        Public Property V1 As Double
            Get
                Return _v1
            End Get
            Set(value As Double)
                _v1 = value
            End Set
        End Property

        Public Property T2 As Double
            Get
                Return _t2
            End Get
            Set(value As Double)
                _t2 = value
            End Set
        End Property

        Public Property T1 As Double
            Get
                Return _t1
            End Get
            Set(value As Double)
                _t1 = value
            End Set
        End Property

        Public Property NBPAVG As Double
            Get
                Return _nbpavg
            End Get
            Set(value As Double)
                _nbpavg = value
            End Set
        End Property

        Public Property SG60 As Double
            Get
                Return _sg60
            End Get
            Set(value As Double)
                _sg60 = value
            End Set
        End Property

        Public Property MW As Double
            Get
                Return _mw
            End Get
            Set(value As Double)
                _mw = value
            End Set
        End Property

        Public Property IsCurve As Boolean
            Get
                Return _iscurve
            End Get
            Set(value As Boolean)
                _iscurve = value
            End Set
        End Property

        Public Property IsBulk As Boolean
            Get
                Return _isbulk
            End Get
            Set(value As Boolean)
                _isbulk = value
            End Set
        End Property

        Public Property Name As String
            Get
                Return _name
            End Get
            Set(value As String)
                _name = value
            End Set
        End Property

        Public Function Clone() As Object Implements ICloneable.Clone
            Return ObjectCopy(Me)
        End Function

        Public Function ObjectCopy(source As Assay) As Assay

            If source Is Nothing Then Return Nothing

            Return New Assay With {
                .Name = source.Name,
                .IsBulk = source.IsBulk,
                .IsCurve = source.IsCurve,
                .MW = source.MW,
                .SG60 = source.SG60,
                .NBPAVG = source.NBPAVG,
                .T1 = source.T1,
                .T2 = source.T2,
                .V1 = source.V1,
                .V2 = source.V2,
                .NBPType = source.NBPType,
                .HasMWCurve = source.HasMWCurve,
                .HasSGCurve = source.HasSGCurve,
                .SGCurveType = source.SGCurveType,
                .HasViscCurves = source.HasViscCurves,
                .CurveBasis = source.CurveBasis,
                .API = source.API,
                .K_API = source.K_API,
                .PX = CloneValues(source.PX),
                .PY_NBP = CloneValues(source.PY_NBP),
                .PY_MW = CloneValues(source.PY_MW),
                .PY_SG = CloneValues(source.PY_SG),
                .PY_V1 = CloneValues(source.PY_V1),
                .PY_V2 = CloneValues(source.PY_V2)
            }

        End Function

        Public Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData
            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            Return True
        End Function

        Public Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData
            Return XMLSerializer.XMLSerializer.Serialize(Me)
        End Function

        Private Shared Function CloneValues(source As ArrayList) As ArrayList
            If source Is Nothing Then Return New ArrayList()
            Return DirectCast(source.Clone(), ArrayList)
        End Function

    End Class

End Namespace
