using CutList.Core;

namespace CutList.Forms
{
    public partial class ResultsForm : Form
    {
        private string filename;
        private string cutMethod;
        private string? materialShape;
        private List<Bin> _originalBins = new List<Bin>();

        public ResultsForm(string filename, string cutMethod, string? materialShape = null)
        {
            InitializeComponent();
            dataGridView1.DrawRowNumbers();
            dataGridView2.DrawRowNumbers();

            this.filename = filename;
            this.cutMethod = cutMethod;
            this.materialShape = materialShape;
        }

        private void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            var selectedGroup = dataGridView1.Rows[e.RowIndex].DataBoundItem as BinGroup;

            if (selectedGroup == null)
                return;

            var representativeBin = selectedGroup.RepresentativeBin;
            binLayoutView1.Bin = representativeBin;
            binLayoutView1.Invalidate();

            dataGridView2.DataSource = representativeBin.Items;
        }

        public List<Bin> Bins
        {
            get { return _originalBins; }
            set
            {
                _originalBins = value;
                var groupedBins = BinGroupingHelper.GroupIdenticalBins(value);
                dataGridView1.DataSource = groupedBins;
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Save();
        }

        public void Save(string filepath)
        {
            var writer = new BinFileSaver(_originalBins)
            {
                CutMethod = cutMethod,
                MaterialShape = materialShape
            };
            writer.SaveBinsToFile(filepath);
        }

        public void Save()
        {
            var s = new SaveFileDialog();

            s.FileName = filename;
            s.Filter = "Text File|*.txt";

            if (s.ShowDialog() == DialogResult.OK)
                Save(s.FileName);
        }
    }
}