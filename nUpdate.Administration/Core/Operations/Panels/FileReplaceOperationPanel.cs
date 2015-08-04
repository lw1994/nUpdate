using nUpdate.Core.Operations;
using System;
using System.Windows.Forms;

namespace nUpdate.Administration.Core.Operations.Panels
{
    public partial class FileReplaceOperationPanel : UserControl, IOperationPanel
    {
        public FileReplaceOperationPanel()
        {
            InitializeComponent();
        }

        #region IOperationPanel

        public nUpdate.Core.Operations.Operation Operation
        {
            get { return new Operation(OperationArea.Files, OperationMethod.Replace, ""); }
        }

        public bool IsValid
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}
