import React from 'react';
import { formatCurrency, formatDate, getMonthKey } from '../financeEngine';

function Dashboard({ accounts, transactions, budgets, goals, debts, forecasts, currentDate, onRefresh }) {
  const currentMonthKey = getMonthKey(currentDate);
  const [year, month] = currentMonthKey.split('-').map(Number);

  // Calculate summary metrics
  const totalBalance = accounts.reduce((sum, acc) => sum + acc.balance, 0);
  const netWorth = accounts.reduce((sum, acc) => {
    if (acc.type === 'credit') return sum - Math.abs(acc.balance);
    return sum + acc.balance;
  }, 0);

  const monthlyIncome = transactions
    .filter(t => {
      const d = new Date(t.date);
      return d.getFullYear() === year && d.getMonth() === month - 1 && t.type === 'income';
    })
    .reduce((sum, t) => sum + t.amount, 0);

  const monthlyExpenses = transactions
    .filter(t => {
      const d = new Date(t.date);
      return d.getFullYear() === year && d.getMonth() === month - 1 && t.type === 'expense';
    })
    .reduce((sum, t) => sum + Math.abs(t.amount), 0);

  const monthlyBalance = monthlyIncome - monthlyExpenses;

  // Budget status
  const budgetStatuses = budgets.map(budget => {
    const spent = transactions
      .filter(t => {
        const d = new Date(t.date);
        return d.getFullYear() === year && 
               d.getMonth() === month - 1 && 
               t.type === 'expense' && 
               t.category === budget.category;
      })
      .reduce((sum, t) => sum + Math.abs(t.amount), 0);

    return {
      ...budget,
      spent,
      remaining: budget.amount - spent,
      percentage: (spent / budget.amount) * 100
    };
  });

  // Recent transactions
  const recentTransactions = transactions.slice(0, 5);

  // Goal progress
  const goalProgress = goals.map(goal => ({
    ...goal,
    percentage: (goal.currentAmount / goal.targetAmount) * 100,
    remaining: goal.targetAmount - goal.currentAmount
  }));

  // Debt summary
  const totalDebt = debts.reduce((sum, d) => sum + d.principal, 0);
  const totalMonthlyPayment = debts.reduce((sum, d) => sum + d.minimumPayment, 0);

  return (
    <div className="space-y-6">
      {/* Summary Cards */}
      <div className="grid grid-4 gap-4">
        <div className="card">
          <div className="text-sm text-gray-500 mb-1">Total Balance</div>
          <div className="text-2xl font-bold text-gray-900">
            {formatCurrency(totalBalance)}
          </div>
          <div className="text-sm text-gray-500 mt-1">
            Across {accounts.length} accounts
          </div>
        </div>

        <div className="card">
          <div className="text-sm text-gray-500 mb-1">Net Worth</div>
          <div className={`text-2xl font-bold ${netWorth >= 0 ? 'text-green-600' : 'text-red-600'}`}>
            {formatCurrency(netWorth)}
          </div>
          <div className="text-sm text-gray-500 mt-1">
            Assets minus liabilities
          </div>
        </div>

        <div className="card">
          <div className="text-sm text-gray-500 mb-1">Monthly Income</div>
          <div className="text-2xl font-bold text-green-600">
            {formatCurrency(monthlyIncome)}
          </div>
          <div className="text-sm text-gray-500 mt-1">
            This month
          </div>
        </div>

        <div className="card">
          <div className="text-sm text-gray-500 mb-1">Monthly Expenses</div>
          <div className="text-2xl font-bold text-red-600">
            {formatCurrency(monthlyExpenses)}
          </div>
          <div className="text-sm text-gray-500 mt-1">
            This month
          </div>
        </div>
      </div>

      {/* Monthly Balance Chart */}
      <div className="card">
        <h3 className="text-lg font-bold mb-4">Monthly Balance Overview</h3>
        <div className="h-48 flex items-end justify-between gap-2">
          {(() => {
            const months = [];
            for (let i = 5; i >= 0; i--) {
              const d = new Date(currentDate);
              d.setMonth(d.getMonth() - i);
              const mKey = getMonthKey(d);
              const [y, m] = mKey.split('-').map(Number);
              
              const income = transactions
                .filter(t => {
                  const td = new Date(t.date);
                  return td.getFullYear() === y && td.getMonth() === m - 1 && t.type === 'income';
                })
                .reduce((sum, t) => sum + t.amount, 0);

              const expenses = transactions
                .filter(t => {
                  const td = new Date(t.date);
                  return td.getFullYear() === y && td.getMonth() === m - 1 && t.type === 'expense';
                })
                .reduce((sum, t) => sum + Math.abs(t.amount), 0);

              const balance = income - expenses;
              const maxVal = Math.max(Math.abs(balance), 100);

              months.push({
                label: d.toLocaleDateString('en-US', { month: 'short' }),
                balance,
                height: Math.min(Math.abs(balance) / maxVal * 100, 100),
                isPositive: balance >= 0
              });
            }
            return months.map((m, i) => (
              <div key={i} className="flex-1 flex flex-col items-center">
                <div className="text-xs text-gray-500 mb-1">
                  {formatCurrency(m.balance)}
                </div>
                <div
                  className={`w-full rounded-t transition-all ${
                    m.isPositive ? 'bg-green-500' : 'bg-red-500'
                  }`}
                  style={{ height: `${Math.max(m.height, 5)}%` }}
                />
                <div className="text-xs text-gray-500 mt-2">{m.label}</div>
              </div>
            ));
          })()}
        </div>
      </div>

      <div className="grid grid-2 gap-6">
        {/* Budget Overview */}
        <div className="card">
          <h3 className="text-lg font-bold mb-4">Budget Status</h3>
          <div className="space-y-4">
            {budgetStatuses.slice(0, 5).map(budget => (
              <div key={budget.id}>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-sm font-medium">{budget.category}</span>
                  <span className={`text-sm ${budget.remaining < 0 ? 'text-red-600' : 'text-gray-600'}`}>
                    {formatCurrency(budget.spent)} / {formatCurrency(budget.amount)}
                  </span>
                </div>
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div
                    className={`h-2 rounded-full transition-all ${
                      budget.percentage > 100 ? 'bg-red-500' :
                      budget.percentage > 80 ? 'bg-yellow-500' : 'bg-green-500'
                    }`}
                    style={{ width: `${Math.min(budget.percentage, 100)}%` }}
                  />
                </div>
                {budget.remaining < 0 && (
                  <div className="text-xs text-red-600 mt-1">
                    Over budget by {formatCurrency(Math.abs(budget.remaining))}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Recent Transactions */}
        <div className="card">
          <h3 className="text-lg font-bold mb-4">Recent Transactions</h3>
          <div className="space-y-3">
            {recentTransactions.map(transaction => (
              <div key={transaction.id} className="flex items-center justify-between p-2 bg-gray-50 rounded">
                <div className="flex-1">
                  <div className="text-sm font-medium">{transaction.description}</div>
                  <div className="text-xs text-gray-500">
                    {transaction.category} • {formatDate(transaction.date)}
                  </div>
                </div>
                <div className={`text-sm font-bold ${
                  transaction.type === 'income' ? 'text-green-600' : 'text-red-600'
                }`}>
                  {transaction.type === 'income' ? '+' : '-'}
                  {formatCurrency(Math.abs(transaction.amount))}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="grid grid-2 gap-6">
        {/* Goals Progress */}
        <div className="card">
          <h3 className="text-lg font-bold mb-4">Goals Progress</h3>
          <div className="space-y-4">
            {goalProgress.slice(0, 3).map(goal => (
              <div key={goal.id}>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-sm font-medium">{goal.name}</span>
                  <span className="text-sm text-gray-600">
                    {formatCurrency(goal.currentAmount)} / {formatCurrency(goal.targetAmount)}
                  </span>
                </div>
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div
                    className="bg-blue-500 h-2 rounded-full transition-all"
                    style={{ width: `${Math.min(goal.percentage, 100)}%` }}
                  />
                </div>
                <div className="text-xs text-gray-500 mt-1">
                  {goal.percentage >= 100 ? '🎉 Goal reached!' : 
                   `${formatCurrency(goal.remaining)} remaining`}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Debt Summary */}
        <div className="card">
          <h3 className="text-lg font-bold mb-4">Debt Overview</h3>
          <div className="space-y-3">
            <div className="flex justify-between items-center p-3 bg-red-50 rounded">
              <div>
                <div className="text-sm text-gray-600">Total Debt</div>
                <div className="text-2xl font-bold text-red-600">
                  {formatCurrency(totalDebt)}
                </div>
              </div>
              <div className="text-right">
                <div className="text-sm text-gray-600">Monthly Payment</div>
                <div className="text-lg font-bold text-gray-900">
                  {formatCurrency(totalMonthlyPayment)}
                </div>
              </div>
            </div>
            {debts.slice(0, 3).map(debt => (
              <div key={debt.id} className="flex justify-between items-center p-2 bg-gray-50 rounded">
                <div>
                  <div className="text-sm font-medium">{debt.name}</div>
                  <div className="text-xs text-gray-500">
                    {debt.interestRate}% APR
                  </div>
                </div>
                <div className="text-sm font-bold text-gray-900">
                  {formatCurrency(debt.principal)}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Quick Actions */}
      <div className="card">
        <h3 className="text-lg font-bold mb-4">Quick Actions</h3>
        <div className="flex gap-3 flex-wrap">
          <button className="btn btn-primary" onClick={onRefresh}>
            🔄 Refresh Data
          </button>
          <button className="btn btn-secondary">
            📊 View Reports
          </button>
          <button className="btn btn-secondary">
            ⚙️ Settings
          </button>
          <button className="btn btn-secondary">
            📤 Export Data
          </button>
        </div>
      </div>
    </div>
  );
}

export default Dashboard;
