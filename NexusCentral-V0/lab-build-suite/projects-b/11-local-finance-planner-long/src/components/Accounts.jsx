import React, { useState } from 'react';
import { AccountManager, formatCurrency } from '../financeEngine';

function Accounts({ accounts, onRefresh }) {
  const [showAddForm, setShowAddForm] = useState(false);
  const [newAccount, setNewAccount] = useState({
    name: '',
    type: 'checking',
    balance: 0,
    currency: 'USD'
  });

  const handleAddAccount = (e) => {
    e.preventDefault();
    AccountManager.addAccount(newAccount);
    setNewAccount({ name: '', type: 'checking', balance: 0, currency: 'USD' });
    setShowAddForm(false);
    onRefresh();
  };

  const handleDeleteAccount = (id) => {
    if (confirm('Are you sure you want to delete this account?')) {
      AccountManager.deleteAccount(id);
      onRefresh();
    }
  };

  const totalBalance = accounts.reduce((sum, acc) => sum + acc.balance, 0);
  const netWorth = accounts.reduce((sum, acc) => {
    if (acc.type === 'credit') return sum - Math.abs(acc.balance);
    return sum + acc.balance;
  }, 0);

  return (
    <div className="space-y-6">
      {/* Summary */}
      <div className="grid grid-2 gap-4">
        <div className="card">
          <div className="text-sm text-gray-500 mb-1">Total Balance</div>
          <div className="text-2xl font-bold text-gray-900">
            {formatCurrency(totalBalance)}
          </div>
        </div>
        <div className="card">
          <div className="text-sm text-gray-500 mb-1">Net Worth</div>
          <div className={`text-2xl font-bold ${netWorth >= 0 ? 'text-green-600' : 'text-red-600'}`}>
            {formatCurrency(netWorth)}
          </div>
        </div>
      </div>

      {/* Add Account Form */}
      {showAddForm && (
        <div className="card">
          <h3 className="text-lg font-bold mb-4">Add New Account</h3>
          <form onSubmit={handleAddAccount} className="space-y-4">
            <div className="grid grid-2 gap-4">
              <div>
                <label className="label">Account Name</label>
                <input
                  type="text"
                  className="input"
                  value={newAccount.name}
                  onChange={(e) => setNewAccount({ ...newAccount, name: e.target.value })}
                  required
                />
              </div>
              <div>
                <label className="label">Account Type</label>
                <select
                  className="input"
                  value={newAccount.type}
                  onChange={(e) => setNewAccount({ ...newAccount, type: e.target.value })}
                >
                  <option value="checking">Checking</option>
                  <option value="savings">Savings</option>
                  <option value="credit">Credit Card</option>
                  <option value="investment">Investment</option>
                </select>
              </div>
            </div>
            <div className="grid grid-2 gap-4">
              <div>
                <label className="label">Initial Balance</label>
                <input
                  type="number"
                  step="0.01"
                  className="input"
                  value={newAccount.balance}
                  onChange={(e) => setNewAccount({ ...newAccount, balance: parseFloat(e.target.value) || 0 })}
                  required
                />
              </div>
              <div>
                <label className="label">Currency</label>
                <select
