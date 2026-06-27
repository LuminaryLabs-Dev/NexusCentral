import React, { useState, useEffect } from 'react';
import {
  AccountManager,
  TransactionManager,
  BudgetManager,
  GoalManager,
  DebtManager,
  ForecastManager,
  formatCurrency,
  formatDate,
  getMonthKey
} from './financeEngine';
import Dashboard from './components/Dashboard';
import Accounts from './components/Accounts';
import Transactions from './components/Transactions';
import Budgets from './components/Budgets';
import Goals from './components/Goals';
import Debts from './components/Debts';
import Forecasts from './components/Forecasts';
import './index.css';

function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [accounts, setAccounts] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [budgets, setBudgets] = useState([]);
  const [goals, setGoals] = useState([]);
  const [debts, setDebts] = useState([]);
  const [forecasts, setForecasts] = useState([]);
  const [currentDate, setCurrentDate] = useState(new Date());

  // Load data on mount
  useEffect(() => {
    refreshData();
  }, []);

  const refreshData = () => {
    setAccounts(AccountManager.getAccounts());
    setTransactions(TransactionManager.getTransactions());
    setBudgets(BudgetManager.getBudgets());
    setGoals(GoalManager.getGoals());
    setDebts(DebtManager.getDebts());
    setForecasts(ForecastManager.getForecasts());
  };

  const handleAddTransaction = (transaction) => {
    TransactionManager.addTransaction(transaction);
    refreshData();
  };

  const handleUpdateTransaction = (id, updates) => {
    TransactionManager.updateTransaction(id, updates);
    refreshData();
  };

  const handleDeleteTransaction = (id) => {
    TransactionManager.deleteTransaction(id);
    refreshData();
  };

  const handleUpdateBudget = (id, updates) => {
    BudgetManager.updateBudget(id, updates);
    refreshData();
  };

  const handleUpdateForecast = (id, updates) => {
    ForecastManager.updateForecast(id, updates);
    refreshData();
  };

  const tabs = [
    { id: 'dashboard', label: 'Dashboard', icon: '📊' },
    { id: 'accounts', label: 'Accounts', icon: '💳' },
    { id: 'transactions', label: 'Transactions', icon: '💰' },
    { id: 'budgets', label: 'Budgets', icon: '📋' },
    { id: 'goals', label: 'Goals', icon: '🎯' },
    { id: 'debts', label: 'Debts', icon: '📉' },
    { id: 'forecasts', label: 'Forecasts', icon: '🔮' }
  ];

  const renderContent = () => {
    switch (activeTab) {
      case 'dashboard':
        return (
          <Dashboard
            accounts={accounts}
            transactions={transactions}
            budgets={budgets}
            goals={goals}
            debts={debts}
            forecasts={forecasts}
            currentDate={currentDate}
            onRefresh={refreshData}
          />
        );
      case 'accounts':
        return (
          <Accounts
            accounts={accounts}
            onRefresh={refreshData}
          />
        );
      case 'transactions':
        return (
          <Transactions
            transactions={transactions}
            accounts={accounts}
            onAdd={handleAddTransaction}
            onUpdate={handleUpdateTransaction}
            onDelete={handleDeleteTransaction}
            onRefresh={refreshData}
          />
        );
      case 'budgets':
        return (
          <Budgets
            budgets={budgets}
            currentDate={currentDate}
            onUpdate={handleUpdateBudget}
            onRefresh={refreshData}
          />
        );
      case 'goals':
        return (
          <Goals
            goals={goals}
            onRefresh={refreshData}
          />
        );
      case 'debts':
        return (
          <Debts
            debts={debts}
            onRefresh={refreshData}
          />
        );
      case 'forecasts':
        return (
          <Forecasts
            forecasts={forecasts}
            onUpdate={handleUpdateForecast}
            onRefresh={refreshData}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm border-b border-gray-200">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-blue-600 rounded-lg flex items-center justify-center text-white text-xl font-bold">
                LL
              </div>
              <div>
                <h1 className="text-xl font-bold text-gray-900">LedgerLens</h1>
                <p className="text-sm text-gray-500">Local Finance Planner</p>
              </div>
            </div>
            <div className="flex items-center gap-4">
              <span className="text-sm text-gray-500">
                {formatDate(currentDate)}
              </span>
              <button
                onClick={refreshData}
                className="btn btn-secondary btn-sm"
              >
                Refresh
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Navigation */}
      <nav className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="container mx-auto px-4">
          <div className="flex gap-1 overflow-x-auto py-2">
            {tabs.map(tab => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors whitespace-nowrap ${
                  activeTab === tab.id
                    ? 'bg-blue-50 text-blue-700'
                    : 'text-gray-600 hover:bg-gray-50'
                }`}
              >
                <span>{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-6">
        {renderContent()}
      </main>

      {/* Footer */}
      <footer className="bg-white border-t border-gray-200 mt-auto">
        <div className="container mx-auto px-4 py-4">
          <p className="text-sm text-gray-500 text-center">
            LedgerLens - Your Personal Finance Manager • Data stored locally in your browser
          </p>
        </div>
      </footer>
    </div>
  );
}

export default App;
