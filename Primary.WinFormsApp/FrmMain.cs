﻿using Primary.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Primary.WinFormsApp
{
    public partial class FrmMain : Form
    {
        private List<string> watchList;
        private Instrument[] _watchedInstruments;
        private DateTime _lastUpdate;
        private Task primaryWebSocket;

        public FrmMain()
        {
            InitializeComponent();
        }

        private async void FrmMain_Load(object sender, EventArgs e)
        {
            if (await Login())
            {
                //var frmArbitrationAnalyzer = new FrmArbitrationAnalyzer();
                //frmArbitrationAnalyzer.MdiParent = this;
                //frmArbitrationAnalyzer.WindowState = FormWindowState.Maximized;
                //frmArbitrationAnalyzer.Show();

                var frmSettlementAnalyzer = new FrmSettlementTermsAnalyzer
                {
                    WindowState = FormWindowState.Maximized
                };
                frmSettlementAnalyzer.Show();
            }
        }

        private async Task<bool> Login()
        {
            var login = new FrmLogin();

            if (login.ShowDialog() == DialogResult.OK)
            {
                var text = Text;
                Text = "Login user...";
                Refresh();
                Argentina.Data.Init(login.BaseUrl);
                var success = await Argentina.Data.Api.Login(login.UserName, login.Password);

                if (success == false)
                {
                    _ = MessageBox.Show("Login Failed", "Login Failed", MessageBoxButtons.OK);
                    return await Login();
                }
                else
                {
                    Properties.Settings.Default.ApiBaseUrl = login.BaseUrl;
                    Properties.Settings.Default.UserName = login.UserName;
                    Properties.Settings.Default.Password = login.Password;
                    Properties.Settings.Default.Save();

                    Text = "Initiliazing Data...";
                    Refresh();
                    await Argentina.Data.Init();

                    Text = "Initiliazing Watchlist...";
                    Refresh();

                    foreach (var item in Argentina.Data.AllInstruments.OrderBy(x => x.InstrumentId.SymbolWithoutPrefix()))
                    {
                        _ = cmbInstruments.Items.Add(item);
                    }

                    WatchInstrumentsWithWebSocket();
                    //backgroundTasks.AddRange(Argentina.Data.WatchWithRestApi(_watchedInstruments));
                    tmrConnection.Enabled = true;
                }
                Text = text;
                return true;
            }
            return false;
        }

        private void WatchInstrumentsWithWebSocket()
        {
            primaryWebSocket?.Dispose();

            InitWatchList();
            _watchedInstruments = Argentina.Data.AllInstruments.Where(ShouldWatch).Select(x => x.InstrumentId).ToArray();

            Argentina.Data.OnMarketData += Data_OnMarketData;

            primaryWebSocket = Argentina.Data.WatchWithWebSocket(_watchedInstruments);
        }

        private void Data_OnMarketData(Instrument instrument, Entries data)
        {
            _lastUpdate = DateTime.Now;
        }

        private void MarketDataClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void InitWatchList()
        {
            //var bonds = new[] { "AL29", "AL30", "AL35", "AE38", "AL41", "GD29", "GD30", "GD35", "GD38", "GD41", "GD46" };
            var owned = Properties.Settings.Default.OwnedTickers.Cast<string>().ToList();
            var arbitration = Properties.Settings.Default.ArbitrationTickers.Cast<string>().ToList();

            var bonds = arbitration.Concat(owned).Distinct();

            watchList = new List<string>();
            foreach (var item in bonds)
            {
                if (item.ContainsMultipleTickers())
                {
                    var pesosDC = item.GetMultipleTickers();
                    foreach (var itemParsed in pesosDC)
                    {
                        var settlementItems = itemParsed.GetAllSettlements();
                        watchList.AddRange(settlementItems);
                    }
                }
                else
                {
                    var allSymbols = item.GetAllMervalSymbols();
                    watchList.AddRange(allSymbols);
                }
            }

            // Caucion
            for (var i = 1; i < 10; i++)
            {
                var caucionTicker = Settlement.GetCaucionPesosTicker(i);
                var caucionInstrument = Argentina.Data.GetInstrumentDetailOrNull(caucionTicker);
                if (caucionInstrument != null)
                {
                    watchList.Add(caucionTicker);
                }
            }

        }

        private bool ShouldWatch(InstrumentDetail instrument)
        {
            return watchList.Contains(instrument.InstrumentId.Symbol);
        }

        private async void loginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _ = await Login();
        }

        private void historicDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frmHistoric = new FrmHistoricData();
            //frmHistoric.MdiParent = this;
            frmHistoric.Show();
        }

        private void buscadorDeArbitrajesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frmArbitrationAnalyzer = new FrmArbitrationAnalyzer();
            //frmArbitrationAnalyzer.MdiParent = this;
            frmArbitrationAnalyzer.Show();

        }

        private void cmbInstruments_SelectedIndexChanged(object sender, EventArgs e)
        {
            var instrument = cmbInstruments.SelectedItem as Instrument;
            var frmMarketData = new FrmMarketData();
            frmMarketData.SetInstrument(instrument);
            //frmMarketData.MdiParent = this;
            frmMarketData.Show();
        }

        private void dolarPricesToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void refreshDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _ = Argentina.Data.RefreshMarketData(_watchedInstruments).ToArray();
        }

        private void buscadorArbitrajesSimplesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frmSettlementTermsAnalyzer = new FrmSettlementTermsAnalyzer();
            //frmSettlementTermsAnalyzer.MdiParent = this;
            frmSettlementTermsAnalyzer.Show();
        }

        private void tmrConnection_Tick(object sender, EventArgs e)
        {
            var dif = DateTime.Now - _lastUpdate;

            var connected = dif.TotalSeconds < 15;

            if (connected)
            {
                Icon = Properties.Resources.green_wifi;
                Text = "Chucho Bot 🤖";
            }
            else
            {
                Icon = Properties.Resources.red_wifi;
                Text = $"Chucho Bot 🤖 - Desconectado (último mensaje: hace {dif.TotalSeconds:#0} segundos)";
            }
        }

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            primaryWebSocket?.Dispose();
            primaryWebSocket = Argentina.Data.WatchWithWebSocket(_watchedInstruments);
        }

        private void compraMEPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new FrmDolarPrice
            {
                Text = "Compra Dolar MEP"
            };
            frm.Setup(x => x.GetDolarMEPTrades(), false);
            frm.Show();

        }

        private void ventaMEPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new FrmDolarPrice
            {
                Text = "Venta Dolar MEP"
            };
            frm.Setup(x => x.GetDolarMEPTrades(), true);
            frm.Show();

        }

        private void compraCCLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new FrmDolarPrice
            {
                Text = "Compra Dolar CCL"
            };
            frm.Setup(x => x.GetDolarCableTrades(), false);
            frm.Show();

        }

        private void ventaCCLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new FrmDolarPrice
            {
                Text = "Venta Dolar CCL"
            };
            frm.Setup(x => x.GetDolarCableTrades(), true);
            frm.Show();

        }

        private void instrumentosParaArbitrajeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new Configuration.FrmStringCollectionEditor
            {
                Text = instrumentosParaArbitrajeToolStripMenuItem.Text,
                Setting = Properties.Settings.Default.ArbitrationTickers
            };

            if (frm.ShowDialog() == DialogResult.OK)
            {
                WatchInstrumentsWithWebSocket();
            }
        }

        private void tickersDCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new Configuration.FrmStringCollectionEditor
            {
                Text = tickersDCToolStripMenuItem.Text,
                Setting = Properties.Settings.Default.TickersDC
            };
            _ = frm.ShowDialog();

        }

        private void accionesCEDEARsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new Configuration.FrmStringCollectionEditor
            {
                Text = accionesCEDEARsToolStripMenuItem.Text,
                Setting = Properties.Settings.Default.AccionesCEDEARs
            };
            _ = frm.ShowDialog();
        }

        private void letrasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new Configuration.FrmStringCollectionEditor
            {
                Text = letrasToolStripMenuItem.Text,
                Setting = Properties.Settings.Default.Letras
            };
            _ = frm.ShowDialog();

        }
    }
}
